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

using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    internal class RecordedExchange : RecordedNamedEntity
    {
        public RecordedExchange(string name) : base(name)
        {
        }

        public IDictionary<string, object> Arguments { get; private set; }
        public bool Durable { get; private set; }
        public bool IsAutoDelete { get; private set; }

        public string Type { get; private set; }

        public void Recover(IModel model)
        {
            model.ExchangeDeclare(Name, Type, Durable, IsAutoDelete, Arguments);
        }

        public override string ToString()
        {
            return $"{GetType().Name}: name = '{Name}', type = '{Type}', durable = {Durable}, autoDelete = {IsAutoDelete}, arguments = '{Arguments}'";
        }

        public RecordedExchange WithArguments(IDictionary<string, object> value)
        {
            Arguments = value;
            return this;
        }

        public RecordedExchange WithAutoDelete(bool value)
        {
            IsAutoDelete = value;
            return this;
        }

        public RecordedExchange WithDurable(bool value)
        {
            Durable = value;
            return this;
        }

        public RecordedExchange WithType(string value)
        {
            Type = value;
            return this;
        }
    }
}
