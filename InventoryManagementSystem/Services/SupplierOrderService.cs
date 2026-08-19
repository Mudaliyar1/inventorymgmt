using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Services
{
    public class SupplierOrderService : ISupplierOrderService
    {
        private readonly ISupplierOrderRepository _orderRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IProductRepository _productRepository;
        private readonly IBrevoEmailService _emailService;
        private readonly IAuditLogService _auditLogService;

        public SupplierOrderService(
            ISupplierOrderRepository orderRepository,
            ISupplierRepository supplierRepository,
            IProductRepository productRepository,
            IBrevoEmailService emailService,
            IAuditLogService auditLogService)
        {
            _orderRepository = orderRepository;
            _supplierRepository = supplierRepository;
            _productRepository = productRepository;
            _emailService = emailService;
            _auditLogService = auditLogService;
        }

        public async Task<SupplierOrder?> GetOrderByIdAsync(string id)
        {
            return await _orderRepository.GetByIdAsync(id);
        }

        public async Task<SupplierOrder?> GetOrderByNumberAsync(string orderNumber)
        {
            return await _orderRepository.GetByOrderNumberAsync(orderNumber);
        }

        public async Task<IEnumerable<SupplierOrder>> GetPagedOrdersAsync(string? search, string? supplierId, string? status, int page, int pageSize)
        {
            return await _orderRepository.GetPagedOrdersAsync(search, supplierId, status, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? supplierId, string? status)
        {
            return await _orderRepository.GetFilteredCountAsync(search, supplierId, status);
        }

        public async Task<IEnumerable<SupplierOrder>> GetSupplierOrdersAsync(string supplierId, string? status, int limit = 50)
        {
            return await _orderRepository.GetSupplierOrdersAsync(supplierId, status, limit);
        }

        public async Task<Dictionary<string, long>> GetOrderStatusCountsAsync(string? supplierId = null)
        {
            return await _orderRepository.GetOrderStatusCountsAsync(supplierId);
        }

        public async Task<(bool Success, string Message, SupplierOrder? Order)> CreateOrderAsync(SupplierOrder order, string executedBy)
        {
            if (order == null) return (false, "Order data is null.", null);
            if (string.IsNullOrWhiteSpace(order.SupplierId)) return (false, "Supplier selection is required.", null);
            if (order.Items == null || !order.Items.Any()) return (false, "Purchase Order must contain at least one product item.", null);

            var supplier = await _supplierRepository.GetByIdAsync(order.SupplierId);
            if (supplier == null) return (false, "Selected supplier vendor record was not found.", null);

            order.SupplierName = supplier.CompanyName;
            order.SupplierEmail = supplier.Email;
            order.SupplierPhone = supplier.Phone;
            order.CreatedBy = executedBy;
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.Status = SupplierOrderStatus.Pending;

            // Generate unique PO-YYYYMMDD-XXXX number
            order.OrderNumber = await _orderRepository.GetNextOrderNumberAsync();

            // Populate & snapshot item details
            int totalQty = 0;
            decimal subtotal = 0;

            foreach (var item in order.Items)
            {
                if (item.Quantity <= 0) item.Quantity = 1;
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    item.ProductName = product.Name;
                    item.Brand = product.Brand;
                    item.Model = product.ModelName;
                    item.Variant = product.Variant;
                    item.Color = product.Color;
                    item.Ram = product.Ram;
                    item.Storage = product.Storage;
                    item.ImageUrl = product.ImageUrl;
                    if (item.UnitPrice <= 0)
                    {
                        item.UnitPrice = product.SupplierPrice > 0 ? product.SupplierPrice : product.PurchasePrice;
                    }
                }

                item.Subtotal = item.Quantity * item.UnitPrice;
                totalQty += item.Quantity;
                subtotal += item.Subtotal;
            }

            order.TotalQuantity = totalQty;
            order.Subtotal = subtotal;
            order.GrandTotal = subtotal + order.Tax - order.Discount;

            // Save to database
            await _orderRepository.CreateAsync(order);

            // Audit log
            await _auditLogService.LogActivityAsync(
                "SUPPLIER_ORDER_CREATED",
                executedBy,
                order.OrderNumber,
                $"Created Purchase Order #{order.OrderNumber} for supplier '{supplier.CompanyName}' with {totalQty} items totaling ₹{order.GrandTotal:N2}.");

            // Brevo Email Notification
            if (!string.IsNullOrWhiteSpace(supplier.Email))
            {
                try
                {
                    var emailSubject = $"New Purchase Order #{order.OrderNumber} from SIMS Mobile Shop";
                    var htmlBuilder = new StringBuilder();
                    htmlBuilder.Append($"<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;background:#0F172A;color:#F8FAFC;padding:24px;border-radius:12px;'>");
                    htmlBuilder.Append($"<h2 style='color:#38BDF8;margin-top:0;'>New Purchase Order Received</h2>");
                    htmlBuilder.Append($"<p>Dear <strong>{supplier.ContactPerson}</strong> ({supplier.CompanyName}),</p>");
                    htmlBuilder.Append($"<p>A new purchase order <strong>#{order.OrderNumber}</strong> has been issued by SIMS Mobile Shop.</p>");
                    htmlBuilder.Append($"<div style='background:#1E293B;padding:16px;border-radius:8px;margin:16px 0;'>");
                    htmlBuilder.Append($"<p style='margin:4px 0;'><strong>Order Number:</strong> {order.OrderNumber}</p>");
                    htmlBuilder.Append($"<p style='margin:4px 0;'><strong>Date:</strong> {order.CreatedAt:dd-MMM-yyyy HH:mm} IST</p>");
                    htmlBuilder.Append($"<p style='margin:4px 0;'><strong>Total Items:</strong> {order.TotalQuantity} units</p>");
                    htmlBuilder.Append($"<p style='margin:4px 0;'><strong>Grand Total:</strong> ₹{order.GrandTotal:N2}</p>");
                    htmlBuilder.Append($"</div>");
                    htmlBuilder.Append($"<h3>Order Summary:</h3>");
                    htmlBuilder.Append($"<table style='width:100%;border-collapse:collapse;color:#F8FAFC;'>");
                    htmlBuilder.Append($"<thead><tr style='background:#334155;text-align:left;'><th style='padding:8px;'>Product</th><th style='padding:8px;'>Qty</th><th style='padding:8px;'>Unit Price</th><th style='padding:8px;'>Total</th></tr></thead><tbody>");

                    foreach (var it in order.Items)
                    {
                        htmlBuilder.Append($"<tr style='border-bottom:1px solid #334155;'>");
                        htmlBuilder.Append($"<td style='padding:8px;'>{it.ProductName} ({it.Variant} {it.Color})</td>");
                        htmlBuilder.Append($"<td style='padding:8px;'>{it.Quantity}</td>");
                        htmlBuilder.Append($"<td style='padding:8px;'>₹{it.UnitPrice:N2}</td>");
                        htmlBuilder.Append($"<td style='padding:8px;'>₹{it.Subtotal:N2}</td>");
                        htmlBuilder.Append($"</tr>");
                    }
                    htmlBuilder.Append($"</tbody></table>");
                    htmlBuilder.Append($"<p style='margin-top:20px;'>Please log into your <strong>SIMS Supplier Portal</strong> to view, accept, and update status for this order.</p>");
                    htmlBuilder.Append($"<div style='margin-top:24px;font-size:12px;color:#94A3B8;'>SIMS Smart Inventory Management System</div>");
                    htmlBuilder.Append($"</div>");

                    var (emailSuccess, messageId, apiResp, errorMsg) = await _emailService.SendTransactionalEmailAsync(supplier.Email, emailSubject, htmlBuilder.ToString());
                    
                    order.EmailSent = emailSuccess;
                    order.EmailSentAt = emailSuccess ? DateTime.UtcNow : null;
                    order.EmailError = errorMsg;
                    await _orderRepository.UpdateAsync(order.Id, order);

                    await _auditLogService.LogActivityAsync(
                        emailSuccess ? "SUPPLIER_ORDER_EMAIL_SENT" : "SUPPLIER_ORDER_EMAIL_FAILED",
                        executedBy,
                        order.OrderNumber,
                        emailSuccess ? $"Email sent to {supplier.Email} (MessageID: {messageId})" : $"Email to {supplier.Email} failed: {errorMsg}");
                }
                catch (Exception ex)
                {
                    order.EmailSent = false;
                    order.EmailError = ex.Message;
                    await _orderRepository.UpdateAsync(order.Id, order);
                }
            }

            return (true, $"Purchase Order #{order.OrderNumber} created successfully!", order);
        }

        public async Task<(bool Success, string Message)> UpdateOrderStatusAsync(string orderId, string newStatus, string updatedBy, string? supplierNotes = null, DateTime? expectedDeliveryDate = null)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return (false, "Order ID is required.");
            if (!SupplierOrderStatus.AllStatuses.Contains(newStatus)) return (false, $"Invalid order status '{newStatus}'.");

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return (false, "Purchase order record not found.");

            var oldStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;
            
            if (supplierNotes != null)
            {
                order.SupplierNotes = supplierNotes;
            }
            if (expectedDeliveryDate.HasValue)
            {
                order.ExpectedDeliveryDate = expectedDeliveryDate.Value;
            }

            await _orderRepository.UpdateAsync(order.Id, order);

            await _auditLogService.LogActivityAsync(
                $"SUPPLIER_ORDER_{newStatus.ToUpper()}",
                updatedBy,
                order.OrderNumber,
                $"Updated PO #{order.OrderNumber} status from '{oldStatus}' to '{newStatus}'. Notes: {supplierNotes ?? "-"}");

            return (true, $"Order #{order.OrderNumber} status updated to '{newStatus}'.");
        }
    }
}
