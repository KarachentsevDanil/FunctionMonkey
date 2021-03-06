﻿using System;
using FunctionMonkey.Abstractions.Builders.Model;

namespace FunctionMonkey.Model
{
    public class ServiceBusSubscriptionFunctionDefinition : AbstractFunctionDefinition
    {
        public ServiceBusSubscriptionFunctionDefinition(Type commandType) : base("SbsFn", commandType)
        {
        }

        public string ConnectionStringName { get; set; }

        public string TopicName { get; set; }

        public string SubscriptionName { get; set; }
    }
}
