﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Wayne Hunsley <whunsley@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using SaslMechanism = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism;
using SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Collection("Kafka")]
    [Trait("Category", "Kafka")]
    public class KafkaProducerAssumeTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducer _producer;
        private readonly IAmAMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();

        public KafkaProducerAssumeTests(ITestOutputHelper output)
        {
            const string groupId = "Kafka Message Producer Assume Topic Test";
            _output = output;
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 3,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 10000,
                    MakeChannels = OnMissingChannel.Assume //This will not make the topic
               }).Create(); 
            
            //This should force creation of the topic - will fail if no topic creation code
            _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                })
                .Create(new KafkaSubscription<MyCommand>(
                     channelName: new ChannelName(_queueName), 
                     routingKey: new RoutingKey(_topic),
                     groupId: groupId,
                     numOfPartitions: 1,
                     replicationFactor: 3,
                     makeChannels: OnMissingChannel.Assume //This will not make the topic
                     )
             );
  
        }

        [Fact]
        public void When_a_consumer_declares_topics()
        {
            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]"));
            
            var failure = false;
            try
            {
                _producer.Send(message);
            }
            catch (ChannelFailureException cfe)
            {
                if (cfe.InnerException is ProduceException<string, string>)
                    failure = true;
            }
            
            //This ought to throw an exception, but the Confluent Container is setup to create topics automatically
            //So it does not matter what we do, the producer will create this is on a send
            Assert.False(failure);
        }

        public void Dispose()
        {
            _producer?.Dispose();
            _consumer?.Dispose();
        }
    }
}
