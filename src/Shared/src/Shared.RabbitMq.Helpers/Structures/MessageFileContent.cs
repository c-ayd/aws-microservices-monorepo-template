#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.Text.Json.Serialization;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.Structures
{
    public class MessageFileContent
    {
        public byte[] WrappedDataKey { get; set; }
        public List<Message> Messages { get; set; }

        // Reserved for JSON deserialization
        [JsonConstructor]
        private MessageFileContent()
        {
        }

        public MessageFileContent(WrappedDataKey wrappedDataKey)
        {
            WrappedDataKey = wrappedDataKey.Value;
            
            Messages = new List<Message>();
            foreach (var rejectedMessage in wrappedDataKey.RejectedMessages)
            {
                Messages.Add(new Message(rejectedMessage));
            }
        }

        public class Message
        {
            public string ExchangeName { get; set; }
            public string RoutingKey { get; set; }
            public string Properties { get; set; }
            public byte[] BodyEncrypted { get; set; }

            private BasicProperties? _basicProperties = null;

            // Reserved for JSON deserialization
            [JsonConstructor]
            private Message()
            {
            }

            public Message(RejectedMessage rejectedMessage)
            {
                ExchangeName = rejectedMessage.ExchangeName;
                RoutingKey = rejectedMessage.RoutingKey;
                Properties = rejectedMessage.Properties;
                BodyEncrypted = rejectedMessage.BodyEncrypted;
            }

            public BasicProperties GetBasicProperties()
            {
                if (_basicProperties == null)
                {
                    _basicProperties = Utility.ConvertStringToBasicProperties(Properties);
                }

                return _basicProperties;
            }
        }
    }
}
