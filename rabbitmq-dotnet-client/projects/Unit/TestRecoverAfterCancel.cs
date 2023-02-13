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
using System.Text;

using NUnit.Framework;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestRecoverAfterCancel
    {
        IConnection Connection;
        IModel Channel;
        string Queue;
        int callbackCount;

        public int ModelNumber(IModel model)
        {
            return ((ModelBase)model).Session.ChannelNumber;
        }

        [SetUp] public void Connect()
        {
            Connection = new ConnectionFactory().CreateConnection();
            Channel = Connection.CreateModel();
            Queue = Channel.QueueDeclare("", false, true, false, null);
        }

        [TearDown] public void Disconnect()
        {
            Connection.Abort();
        }

        [Test]
        public void TestRecoverAfterCancel_()
        {
            UTF8Encoding enc = new UTF8Encoding();
            Channel.BasicPublish("", Queue, null, enc.GetBytes("message"));
            EventingBasicConsumer Consumer = new EventingBasicConsumer(Channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer.Received += (_, e) => EventQueue.Enqueue((e.Redelivered, e.Body.ToArray()));

            string CTag = Channel.BasicConsume(Queue, false, Consumer);
            (bool Redelivered, byte[] Body) Event = EventQueue.Dequeue();
            Channel.BasicCancel(CTag);
            Channel.BasicRecover(true);

            EventingBasicConsumer Consumer2 = new EventingBasicConsumer(Channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue2 = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer2.Received += (_, e) => EventQueue2.Enqueue((e.Redelivered, e.Body.ToArray()));
            Channel.BasicConsume(Queue, false, Consumer2);
            (bool Redelivered, byte[] Body) Event2 = EventQueue2.Dequeue();

            CollectionAssert.AreEqual(Event.Body, Event2.Body);
            Assert.IsFalse(Event.Redelivered);
            Assert.IsTrue(Event2.Redelivered);
        }

        [Test]
        public void TestRecoverCallback()
        {
            callbackCount = 0;
            Channel.BasicRecoverOk += IncrCallback;
            Channel.BasicRecover(true);
            Assert.AreEqual(1, callbackCount);
        }

        void IncrCallback(object sender, EventArgs args)
        {
            callbackCount++;
        }

    }
}
