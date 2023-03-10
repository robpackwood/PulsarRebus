// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

using NUnit.Framework;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture
    {
        private readonly byte[] _messageBody;
        private readonly ushort _totalMessageCount = 1024;
        private readonly ushort _closeAtCount = 16;
        private string _queueName;

        public TestConnectionRecovery()
        {
            var rnd = new Random();
            _messageBody = new byte[4096];
            rnd.NextBytes(_messageBody);
        }

        [SetUp]
        public override void Init()
        {
            _queueName = $"TestConnectionRecovery-queue-{Guid.NewGuid()}";
            Conn = CreateAutorecoveringConnection();
            Model = Conn.CreateModel();
            Model.QueueDelete(_queueName);
        }

        [TearDown]
        public void CleanUp()
        {
            Conn.Close();
        }

        [Test]
        public void TestBasicAckAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(Model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = Model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.AreEqual(queueName, _queueName);

            Model.BasicQos(0, 1, false);
            string consumerTag = Model.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(Conn);
            ManualResetEventSlim rl = PrepareForRecovery(Conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl);
            Wait(rl);
            Wait(allMessagesSeenLatch);
        }

        [Test]
        public void TestBasicNackAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(Model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = Model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.AreEqual(queueName, _queueName);

            Model.BasicQos(0, 1, false);
            string consumerTag = Model.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(Conn);
            ManualResetEventSlim rl = PrepareForRecovery(Conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl);
            Wait(rl);
            Wait(allMessagesSeenLatch);
        }

        [Test]
        public void TestBasicRejectAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new RejectingBasicConsumer(Model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = Model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.AreEqual(queueName, _queueName);

            Model.BasicQos(0, 1, false);
            string consumerTag = Model.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(Conn);
            ManualResetEventSlim rl = PrepareForRecovery(Conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl);
            Wait(rl);
            Wait(allMessagesSeenLatch);
        }

        [Test]
        public void TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            Model.QueueDeclare(q, false, false, false, null);
            // create an offset
            IBasicProperties bp = Model.CreateBasicProperties();
            Model.BasicPublish("", q, bp, _messageBody);
            Thread.Sleep(50);
            BasicGetResult g = Model.BasicGet(q, false);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
            Assert.IsTrue(Model.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            Model.BasicAck(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception 
            Model.BasicGet(q, false);
        }

        [Test]
        public void TestBasicAckEventHandlerRecovery()
        {
            Model.ConfirmSelect();
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringModel)Model).BasicAcks += (m, args) => latch.Set();
            ((AutorecoveringModel)Model).BasicNacks += (m, args) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            WithTemporaryNonExclusiveQueue(Model, (m, q) => m.BasicPublish("", q, null, _messageBody));
            Wait(latch);
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryWithEndpointList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryStopsAfterManualClose()
        {
            Assert.IsTrue(Conn.IsOpen);
            AutorecoveringConnection c = CreateAutorecoveringConnection();
            var latch = new AutoResetEvent(false);
            c.ConnectionRecoveryError += (o, args) => latch.Set();
            StopRabbitMQ();
            latch.WaitOne(30000); // we got the failed reconnection event.
            bool triedRecoveryAfterClose = false;
            c.Close();
            Thread.Sleep(5000);
            c.ConnectionRecoveryError += (o, args) => triedRecoveryAfterClose = true;
            Thread.Sleep(10000);
            Assert.IsFalse(triedRecoveryAfterClose);
            StartRabbitMQ();
        }

        [Test]
        public void TestBasicConnectionRecoveryWithEndpointListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("191.72.44.22"),
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.IsTrue(Conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestBasicModelRecovery()
        {
            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
        }

        [Test]
        public void TestBasicModelRecoveryOnServerRestart()
        {
            Assert.IsTrue(Model.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
        }

        [Test]
        public void TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            Conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Wait(latch);

            Unblock();
        }

        [Test]
        public void TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(Model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public void TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            WithTemporaryQueueNoWait(Model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
            }, s);
        }

        [Test]
        public void TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(Model, (m, q) =>
            {
                RestartServerAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public void TestConsumerWorkServiceRecovery()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IModel m = c.CreateModel();
                string q = m.QueueDeclare("dotnet-client.recovery.consumer_work_pool1",
                    false, false, false, null).QueueName;
                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q, true, cons);
                AssertConsumerCount(m, q, 1);

                CloseAndWaitForRecovery(c);

                Assert.IsTrue(m.IsOpen);
                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q, null, encoding.GetBytes("msg"));
                Wait(latch);

                m.QueueDelete(q);
            }
        }

        [Test]
        public void TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IModel m = c.CreateModel();
                string q1 = m.QueueDeclare(q0, false, false, false, null).QueueName;
                Assert.AreEqual(q0, q1);

                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q1, true, cons);
                AssertConsumerCount(m, q1, 1);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangeAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q1, null, encoding.GetBytes("msg"));
                Wait(latch);

                m.QueueDelete(q1);
            }
        }

        [Test]
        public void TestConsumerRecoveryWithManyConsumers()
        {
            string q = Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(Model);
                Model.BasicConsume(q, true, cons);
            }

            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)Conn).ConsumerTagChangeAfterRecovery += (prev, current) => latch.Set();

            CloseAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(Model.IsOpen);
            AssertConsumerCount(q, n);
        }

        [Test]
        public void TestCreateModelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            AutorecoveringConnection c = CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                c.Close();
                WaitForShutdown(c);
                Assert.IsFalse(c.IsOpen);
                c.CreateModel();
                Assert.Fail("Expected an exception");
            }
            catch (AlreadyClosedException)
            {
                // expected
            }
            finally
            {
                StartRabbitMQ();
                if (c.IsOpen)
                {
                    c.Abort();
                }
            }
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                Model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                Model.ExchangeDeclare(x2, "fanout", false, false, null);
                Model.ExchangeBind(x2, x1, "");
                Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                Model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                Model.ExchangeDeclare(x2, "fanout", false, false, null);
                Model.ExchangeBind(x2, x1, "");
                Model.ExchangeUnbind(x2, x1, "");
                Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = Model.QueueDeclare();
                Model.QueueBind(q, x, "");
                Model.QueueDelete(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = Model.QueueDeclare();
                Model.QueueBind(q, x, "");
                Model.QueueUnbind(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                Model.QueueDeclare(q, false, false, true, null);
                var dummy = new EventingBasicConsumer(Model);
                string tag = Model.BasicConsume(q, true, dummy);
                Model.BasicCancel(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            DeclareNonDurableExchange(Model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(Model, x);
            Model.ExchangeDelete(x);
        }

        [Test]
        public void TestExchangeRecoveryWithNoWait()
        {
            string x = "dotnet-client.test.recovery.x1-nowait";
            DeclareNonDurableExchangeNoWait(Model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(Model, x);
            Model.ExchangeDelete(x);
        }

        [Test]
        public void TestExchangeToExchangeBindingRecovery()
        {
            string q = Model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                AssertMessageCount(q, 1);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                qs.Add(Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            foreach (string q in qs)
            {
                AssertQueueRecovery(Model, q, false);
                Model.QueueDelete(q);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public void TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IModel ch = Conn.CreateModel();
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            ch.QueueBind(queue: q, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(ch.IsOpen);
            ch.ConfirmSelect();
            ch.QueuePurge(q);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            QueueDeclareOk ok = ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            Assert.AreEqual(1, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public void TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            IModel ch = Conn.CreateModel();
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            string q = ch.QueueDeclare(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)Conn).QueueNameChangeAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            ch.QueueBind(queue: nameBefore, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(ch.IsOpen);
            Assert.AreNotEqual(nameBefore, nameAfter);
            ch.ConfirmSelect();
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            QueueDeclareOk ok = ch.QueueDeclarePassive(nameAfter);
            Assert.AreEqual(1, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        [Test]
        public void TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringModel)Model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestRecoveryEventHandlersOnConnection()
        {
            int counter = 0;
            ((AutorecoveringConnection)Conn).RecoverySucceeded += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnModel()
        {
            int counter = 0;
            ((AutorecoveringModel)Model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IModel ch = conn.CreateModel();
            string s = "dotnet-client.test.recovery.q2";
            ch.QueueDelete(s);
            ch.QueueDeclare(s, false, true, false, null);
            ch.QueueDeclarePassive(s);
            Assert.IsTrue(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.IsTrue(ch.IsOpen);
                ch.QueueDeclarePassive(s);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException)
            {
                // expected
            }
            finally
            {
                conn.Abort();
            }
        }

        [Test]
        public void TestServerNamedQueueRecovery()
        {
            string q = Model.QueueDeclare("", false, false, false, null).QueueName;
            string x = "amq.fanout";
            Model.QueueBind(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var latch = new ManualResetEventSlim(false);
            var connection = (AutorecoveringConnection)Conn;
            connection.RecoverySucceeded += (source, ea) => latch.Set();
            connection.QueueNameChangeAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            CloseAndWaitForRecovery();
            Wait(latch);

            Assert.IsNotNull(nameAfter);
            Assert.IsTrue(nameBefore.StartsWith("amq."));
            Assert.IsTrue(nameAfter.StartsWith("amq."));
            Assert.AreNotEqual(nameBefore, nameAfter);

            Model.QueueDeclarePassive(nameAfter);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            Conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            Conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);
            ManualResetEventSlim shutdownLatch = PrepareForShutdown(Conn);
            ManualResetEventSlim recoveryLatch = PrepareForRecovery((AutorecoveringConnection)Conn);

            Assert.IsTrue(Conn.IsOpen);
            StopRabbitMQ();
            Console.WriteLine("Stopped RabbitMQ. About to sleep for multiple recovery intervals...");
            Thread.Sleep(7000);
            StartRabbitMQ();
            Wait(shutdownLatch, TimeSpan.FromSeconds(30));
            Wait(recoveryLatch, TimeSpan.FromSeconds(30));
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnModel()
        {
            int counter = 0;
            Model.ModelShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoverTopologyOnDisposedChannel()
        {
            string x = GenerateExchangeName();
            string q = GenerateQueueName();
            const string rk = "routing-key";

            using (IModel m = Conn.CreateModel())
            {
                m.ExchangeDeclare(exchange: x, type: "fanout");
                m.QueueDeclare(q, false, false, false, null);
                m.QueueBind(q, x, rk);
            }

            var cons = new EventingBasicConsumer(Model);
            Model.BasicConsume(q, true, cons);
            AssertConsumerCount(Model, q, 1);

            CloseAndWaitForRecovery();
            AssertConsumerCount(Model, q, 1);

            var latch = new ManualResetEventSlim(false);
            cons.Received += (s, args) => latch.Set();

            Model.BasicPublish("", q, null, _messageBody);
            Wait(latch);

            Model.QueueUnbind(q, x, rk);
            Model.ExchangeDelete(x);
            Model.QueueDelete(q);
        }

        [Test]
        public void TestPublishRpcRightAfterReconnect()
        {
            string testQueueName = $"dotnet-client.test.{nameof(TestPublishRpcRightAfterReconnect)}";
            Model.QueueDeclare(testQueueName, false, false, false, null);
            var replyConsumer = new EventingBasicConsumer(Model);
            Model.BasicConsume("amq.rabbitmq.reply-to", true, replyConsumer);
            var properties = Model.CreateBasicProperties();
            properties.ReplyTo = "amq.rabbitmq.reply-to";

            TimeSpan doneSpan = TimeSpan.FromMilliseconds(100);
            var done = new ManualResetEventSlim(false);
            var t = new Thread(() =>
            {
                try
                {

                    CloseAndWaitForRecovery();
                }
                finally
                {
                    done.Set();
                }
            });
            t.Start();

            while (!done.IsSet)
            {
                try
                {
                    Model.BasicPublish(string.Empty, testQueueName, false, properties, _messageBody);
                }
                catch (Exception e)
                {
                    if (e is AlreadyClosedException a)
                    {
                        // 406 is received, when the reply consumer isn't yet recovered
                        Assert.AreNotEqual(406, a.ShutdownReason.ReplyCode);
                    }
                }
                done.Wait(doneSpan);
            }
            t.Join();
        }

        [Test]
        public void TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(Model);
                string tag = Model.BasicConsume(q, true, cons);
                Model.BasicCancel(tag);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            AssertConsumerCount(q, 0);
        }

        [Test]
        public void TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = Model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");
            Model.ExchangeUnbind(x1, x2, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            Model.ExchangeDeclare(x, "fanout");
            Model.ExchangeDelete(x);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.ExchangeDeclarePassive(x);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public void TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = Model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");
            Model.QueueUnbind(q, x1, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            Model.QueueDeclare(q, false, false, false, null);
            Model.QueueDelete(q);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.QueueDeclarePassive(q);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public void TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            Conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Unblock();
            Wait(latch);
        }

        internal void AssertExchangeRecovery(IModel m, string x)
        {
            m.ConfirmSelect();
            WithTemporaryNonExclusiveQueue(m, (_, q) =>
            {
                string rk = "routing-key";
                m.QueueBind(q, x, rk);
                m.BasicPublish(x, rk, null, _messageBody);

                Assert.IsTrue(WaitForConfirms(m));
                m.ExchangeDeclarePassive(x);
            });
        }

        internal void AssertQueueRecovery(IModel m, string q)
        {
            AssertQueueRecovery(m, q, true);
        }

        internal void AssertQueueRecovery(IModel m, string q, bool exclusive)
        {
            m.ConfirmSelect();
            m.QueueDeclarePassive(q);
            QueueDeclareOk ok1 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok1.MessageCount, 0);
            m.BasicPublish("", q, null, _messageBody);
            Assert.IsTrue(WaitForConfirms(m));
            QueueDeclareOk ok2 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok2.MessageCount, 1);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedExchangesCount);
        }

        internal void AssertRecordedQueues(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedQueuesCount);
        }

        internal void CloseAllAndWaitForRecovery()
        {
            CloseAllAndWaitForRecovery((AutorecoveringConnection)Conn);
        }

        internal void CloseAllAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseAllConnections();
            Wait(rl);
        }

        internal void CloseAndWaitForRecovery()
        {
            CloseAndWaitForRecovery((AutorecoveringConnection)Conn);
        }

        internal void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl);
            Wait(rl);
        }

        internal void CloseAndWaitForShutdown(IAutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            CloseConnection(conn);
            Wait(sl);
        }

        internal ManualResetEventSlim PrepareForRecovery(IConnection conn)
        {
            var latch = new ManualResetEventSlim(false);

            IAutorecoveringConnection aconn = conn as IAutorecoveringConnection;
            aconn.RecoverySucceeded += (source, ea) => latch.Set();

            return latch;
        }

        internal ManualResetEventSlim PrepareForShutdown(IConnection conn)
        {
            var latch = new ManualResetEventSlim(false);

            IAutorecoveringConnection aconn = conn as IAutorecoveringConnection;
            aconn.ConnectionShutdown += (c, args) => latch.Set();

            return latch;
        }

        protected override void ReleaseResources()
        {
            Unblock();
        }

        internal void RestartServerAndWaitForRecovery()
        {
            RestartServerAndWaitForRecovery((IAutorecoveringConnection)Conn);
        }

        internal void RestartServerAndWaitForRecovery(IAutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            RestartRabbitMQ();
            Wait(sl);
            Wait(rl);
        }

        internal void WaitForRecovery()
        {
            Wait(PrepareForRecovery((AutorecoveringConnection)Conn));
        }

        internal void WaitForRecovery(AutorecoveringConnection conn)
        {
            Wait(PrepareForRecovery(conn));
        }

        internal void WaitForShutdown()
        {
            Wait(PrepareForShutdown(Conn));
        }

        internal void WaitForShutdown(IConnection conn)
        {
            Wait(PrepareForShutdown(conn));
        }

        internal void PublishMessagesWhileClosingConn(string queueName)
        {
            using (IAutorecoveringConnection publishingConn = CreateAutorecoveringConnection())
            {
                using (IModel publishingModel = publishingConn.CreateModel())
                {
                    for (ushort i = 0; i < _totalMessageCount; i++)
                    {
                        if (i == _closeAtCount)
                        {
                            CloseConnection(Conn);
                        }
                        publishingModel.BasicPublish(string.Empty, queueName, null, _messageBody);
                    }
                }
            }
        }

        public class AckingBasicConsumer : TestBasicConsumer
        {
            public AckingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicAck(deliveryTag, false);
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer
        {
            public NackingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicNack(deliveryTag, false, false);
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer
        {
            public RejectingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicReject(deliveryTag, false);
            }
        }

        public class TestBasicConsumer : DefaultBasicConsumer
        {
            private readonly ManualResetEventSlim _allMessagesSeenLatch;
            private readonly ushort _totalMessageCount;
            private ushort _counter = 0;

            public TestBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model)
            {
                _totalMessageCount = totalMessageCount;
                _allMessagesSeenLatch = allMessagesSeenLatch;
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                try
                {
                    PostHandleDelivery(deliveryTag);
                }
                finally
                {
                    ++_counter;
                    if (_counter >= _totalMessageCount)
                    {
                        _allMessagesSeenLatch.Set();
                    }
                }
            }

            public virtual void PostHandleDelivery(ulong deliveryTag)
            {
            }
        }
    }
}

#pragma warning restore 0168
