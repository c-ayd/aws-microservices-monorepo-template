using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace Shared.RabbitMq.Helpers.Structures
{
    public class MessageFileContent
    {
        public byte[] WrappedDataKey { get; set; }
        public List<Message> Messages { get; set; }

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

            public Message(RejectedMessage rejectedMessage)
            {
                ExchangeName = rejectedMessage.ExchangeName;
                RoutingKey = rejectedMessage.RoutingKey;
                Properties = rejectedMessage.Properties;
                BodyEncrypted = rejectedMessage.BodyEncrypted;
            }
        }
    }
}
