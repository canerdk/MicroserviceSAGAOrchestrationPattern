using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.API.DataAccess;
using Order.API.DTOs;
using Order.API.Models;
using Shared;
using Shared.Events;
using Shared.Interfaces;
using Shared.Messages;

namespace Order.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly ISendEndpointProvider _sendEndpointProvider;

        public OrdersController(AppDbContext context, ISendEndpointProvider sendEndpointProvider)
        {
            _context = context;
            _sendEndpointProvider = sendEndpointProvider;
        }

        [HttpPost]
        public async Task<IActionResult> Create(OrderCreateDto order)
        {
            var newOrder = new Models.Order()
            {
                BuyerId = order.BuyerId,
                Status = OrderStatus.Suspend,
                Address = new Address() { Line = order.Address.Line, District = order.Address.District , Province = order.Address.Province},
                CreatedDate = DateTime.Now
            };

            order.OrderItems.ForEach(item =>
            {
                newOrder.Items.Add(new OrderItem() { Price = item.Price, ProductId = item.ProductId, Count = item.Count });
            });

            await _context.AddAsync(newOrder);
            await _context.SaveChangesAsync();

            var orderCreatedRequestEvent = new OrderCreatedRequestEvent()
            {
                BuyerId = order.BuyerId,
                OrderId = newOrder.Id,
                Payment = new PaymentMessage() { CardName = order.Payment.CardName, CardNumber = order.Payment.CardNumber, CVV = order.Payment.CVV, Expiration = order.Payment.Expiration, TotalPrice = order.OrderItems.Sum(x => x.Price * x.Count) }
            };

            order.OrderItems.ForEach(item =>
            {
                orderCreatedRequestEvent.OrderItems.Add(new OrderItemMessage() { Count = item.Count, ProductId = item.ProductId });
            });

            var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{RabbitMQSettings.OrderSaga}"));

            await sendEndpoint.Send<IOrderCreatedRequestEvent>(orderCreatedRequestEvent);

            return Ok();
        }
    }
}
