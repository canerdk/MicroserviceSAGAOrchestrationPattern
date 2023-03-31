using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared;
using Shared.Events;
using Shared.Interfaces;
using Stock.API.DataAccess;

namespace Stock.API.Consumers
{
    public class OrderCreatedEventConsumer : IConsumer<IOrderCreatedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderCreatedEventConsumer> _logger;
        private readonly ISendEndpointProvider _sendEndpoint;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderCreatedEventConsumer(AppDbContext context, ILogger<OrderCreatedEventConsumer> logger, ISendEndpointProvider sendEndpoint, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _logger = logger;
            _sendEndpoint = sendEndpoint;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<IOrderCreatedEvent> context)
        {
            var stockResult = new List<bool>();

            foreach (var item in context.Message.OrderItems)
            {
                stockResult.Add(await _context.Stocks.AnyAsync(x => x.ProductId == item.ProductId && x.Count > item.Count));
            }

            if(stockResult.All(x => x.Equals(true)))
            {
                foreach (var item in context.Message.OrderItems)
                {
                    var stock = await _context.Stocks.FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

                    if(stock != null)
                    {
                        stock.Count -= item.Count;
                    }

                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Stock was reserved for BuyerId:{context.Message.CorrelationId}");

                var stockReservedEvent = new StockReservedEvent(context.Message.CorrelationId)
                {
                    OrderItems = context.Message.OrderItems
                };

                await _publishEndpoint.Publish(stockReservedEvent);
            }
            else
            {
                var stockNotReserved = new StockNotReservedEvent(context.Message.CorrelationId)
                {
                    Reason = "Not enough stock"
                };

                _logger.LogInformation("Stock not reserved");

                await _publishEndpoint.Publish(stockNotReserved);
            }


        }
    }
}
