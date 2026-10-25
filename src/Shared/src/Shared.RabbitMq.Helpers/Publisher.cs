using System.Collections.Concurrent;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMq.Helpers.Structures;

namespace Shared.RabbitMq.Helpers
{
    /// <summary>
    /// Handles the RabbitMQ channel and publisher confirm events.
    /// </summary>
    public abstract class Publisher
    {
        public string ServiceName { get; private set; }
        public string PublisherName { get; private set; }

        public IChannel? Channel { get; private set; }
        private SemaphoreSlim _channelSemaphore = new SemaphoreSlim(1, 1);

        internal ConcurrentDictionary<ulong, Message> PendingMessages { get; private set; }
        internal ConcurrentDictionary<int, Message> DroppedMessages { get; private set; }

        internal int MaxRetry { get; private set; }

        public Publisher(string serviceName, string publisherName, int maxRetryForMesages)
        {
            ServiceName = serviceName;
            PublisherName = publisherName;

            Channel = null;
            PendingMessages = new ConcurrentDictionary<ulong, Message>();
            DroppedMessages = new ConcurrentDictionary<int, Message>();
            MaxRetry = maxRetryForMesages;
        }

        internal async Task InitializeAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            if (Channel != null)
            {
                if (Channel.IsOpen)
                {
                    await Channel.CloseAsync();
                }

                await Channel.DisposeAsync();
            }

            Channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: false));

            Channel.BasicAcksAsync += HandleAcknowledgedMessagesAsync;
            Channel.BasicNacksAsync += HandleNotAcknowledgedMessagesAsync;
            Channel.BasicReturnAsync += HandleReturnedMessagesAsync;

            // Since the channel was closed and is renewed now, the events for the current pending messages will not fired.
            // The delivery tags will also start from 1 again, meaning the delivery tags in the pending messages become
            // stale data. Therefore, the pending messages are treated as dropped messages. In case there are messages that
            // are saved in RabbitMQ queues but are not removed from the pending messages in this time window, the consume
            // should handle the duplicate messages if it happens.
            if (PendingMessages.Count > 0)
            {
                foreach (var message in PendingMessages)
                {
                    DroppedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                }
            }

            await DeclareExchangesAsync(cancellationToken);
        }

        /// <summary>
        /// Declares exchanges. Use the <see cref="Publisher.Channel"/> property to declare the exchanges.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the declarations</param>
        protected abstract Task DeclareExchangesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Publishes a message.
        /// </summary>
        /// <param name="exchangeName">Name of the exchange to send the message to</param>
        /// <param name="routingKey">Routing key that is used by the exchange</param>
        /// <param name="properties">Properties of the message</param>
        /// <param name="body">Body of the message</param>
        /// <param name="cancellationToken">Token to cancel the publishing process</param>
        public async Task PublishMessageAsync(
            string exchangeName,
            string routingKey,
            BasicProperties properties,
            byte[] body,
            CancellationToken cancellationToken = default)
        {
            var message = new Message(ServiceName, PublisherName, exchangeName, routingKey, properties, body);

            // To standardize the type of the header values for consumers, the header values are converted to
            // JSON strings. By doing this, when dropped messages are saved somewhere and are sent again later,
            // consumers do not need to know the source. They can directly threat all header values as JSON strings.
            // This not just allows publishers to add any value type to the headers without conversions, but also
            // services trying to resend dropped messages can directly use the header values without conversions.
            if (properties.Headers != null)
            {
                foreach (var header in properties.Headers)
                {
                    properties.Headers[header.Key] = JsonSerializer.Serialize(header.Value);
                }
            }

            await PublishMessageAsync(message, cancellationToken);
        }

        internal async Task PublishMessageAsync(Message message, CancellationToken cancellationToken)
        {
            await _channelSemaphore.WaitAsync(cancellationToken);

            if (Channel == null || !Channel.IsOpen)
            {
                DroppedMessages.TryAdd(message.GetHashCode(), message);
                
                _channelSemaphore.Release();
                return;
            }

            ulong? deliveryTag = null;
            try
            {
                deliveryTag = await Channel!.GetNextPublishSequenceNumberAsync(cancellationToken);

                DroppedMessages.TryRemove(message.GetHashCode(), out var _);
                PendingMessages.TryRemove(message.DeliveryTag, out var _);

                message.DeliveryTag = deliveryTag.Value;
                PendingMessages.TryAdd(deliveryTag.Value, message);

                await Channel.BasicPublishAsync(
                    exchange: message.ExchangeName,
                    routingKey: message.RoutingKey,
                    mandatory: true,
                    basicProperties: message.Properties,
                    body: message.Body);
            }
            catch (Exception)
            {
                if (deliveryTag != null)
                {
                    PendingMessages.TryRemove(deliveryTag.Value, out var _);
                }

                DroppedMessages.TryAdd(message.GetHashCode(), message);
            }
            finally
            {
                _channelSemaphore.Release();
            }
        }

        private async Task HandleAcknowledgedMessagesAsync(object obj, BasicAckEventArgs args)
        {
            if (args.Multiple)
            {
                for (ulong i = args.DeliveryTag; i > 0; --i)
                {
                    if (!PendingMessages.TryRemove(i, out var _))
                        break;
                }
            }
            else
            {
                PendingMessages.TryRemove(args.DeliveryTag, out var _);
            }
        }

        private async Task HandleNotAcknowledgedMessagesAsync(object obj, BasicNackEventArgs args)
        {
            if (args.Multiple)
            {
                for (ulong i = args.DeliveryTag; i > 0; --i)
                {
                    if (!PendingMessages.TryRemove(i, out var message))
                        break;

                    DroppedMessages.TryAdd(message.GetHashCode(), message);
                }
            }
            else
            {
                if (PendingMessages.TryRemove(args.DeliveryTag, out var message))
                {
                    DroppedMessages.TryAdd(message.GetHashCode(), message);
                }
            }
        }

        private async Task HandleReturnedMessagesAsync(object obj, BasicReturnEventArgs args)
        {
            var properties = new BasicProperties(args.BasicProperties);
            var message = new Message(ServiceName, PublisherName, args.Exchange, args.RoutingKey, properties, args.Body.ToArray());

            DroppedMessages.TryAdd(message.GetHashCode(), message);
        }
    }
}
