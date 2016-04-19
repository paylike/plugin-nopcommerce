using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Paylike.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Payments;
using Nop.Services.Localization;
using Paylike.NET.Interfaces;
using Paylike.NET;
using Paylike.NET.RequestModels.Transactions;
using System.Text;
using System.Web;
using Paylike.NET.ResponseModels.Apps;
using Paylike.NET.Entities;
using Paylike.NET.RequestModels.Merchants;

namespace Nop.Plugin.Payments.Paylike
{
    public class PaylikeProcessor : BasePlugin, IPaymentMethod
    {
        private readonly PaylikePaymentSettings _paylikePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly CurrencySettings _currencySettings;
        private readonly IOrderService _orderService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPaylikeTransactionService _paylikeTransactionService;
        private readonly HttpContextBase _httpContext;

        public PaylikeProcessor(ISettingService settingService,
            ICurrencyService currencyService, ICustomerService customerService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            IOrderTotalCalculationService orderTotalCalculationService,
            IOrderService orderService,
            PaylikePaymentSettings paylikePaymentSettings,
            HttpContextBase httpContext)
        {
            _paylikePaymentSettings = paylikePaymentSettings;
            _settingService = settingService;
            _currencyService = currencyService;
            _customerService = customerService;
            _currencySettings = currencySettings;
            _webHelper = webHelper;
            _orderTotalCalculationService = orderTotalCalculationService;
            _orderService = orderService;
            _httpContext = httpContext;

            if (!paylikePaymentSettings.IsValid())
                return;

            _paylikeTransactionService = new PaylikeTransactionService(paylikePaymentSettings.AppKey);
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            var token = processPaymentRequest.CustomValues["paymenttoken"].ToString();
            var primaryStoreCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
            int amount = (int)(Decimal.Round(processPaymentRequest.OrderTotal, 2) * 100);

            CreateTransactionRequest createTransactionRequest = new CreateTransactionRequest()
            {
                Amount = amount,
                Currency = primaryStoreCurrency.CurrencyCode,
                CardId = token,
                MerchantId = _paylikePaymentSettings.MerchantId,
                Descriptor = string.Empty,
                Custom = new Dictionary<string, string>() { }
            };

            createTransactionRequest.Custom.Add("NopOrderGuid", processPaymentRequest.OrderGuid.ToString());

            var createTransactionResponse = _paylikeTransactionService.CreateTransaction(createTransactionRequest);
            if (createTransactionResponse.IsError)
            {
                result.AddError(createTransactionResponse.ErrorMessage);
                result.AddError(createTransactionResponse.ErrorContent);
            }
            else
            {
                result.AuthorizationTransactionId = createTransactionResponse.Content.Id;
                result.NewPaymentStatus = PaymentStatus.Authorized;
            }

            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
        }

        public bool HidePaymentMethod(IList<Core.Domain.Orders.ShoppingCartItem> cart)
        {
            return !_paylikePaymentSettings.IsValid();
        }

        public decimal GetAdditionalHandlingFee(IList<Core.Domain.Orders.ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart, 0, false);
            return result;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var primaryStoreCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);

            var captureRequest = new CaptureTransactionRequest() {
                Amount = (int)(Math.Round(capturePaymentRequest.Order.OrderTotal, 2) * 100),
                Currency = primaryStoreCurrency.CurrencyCode,
                Descriptor = _paylikePaymentSettings.CaptureDescriptor,
                TransactionId = capturePaymentRequest.Order.AuthorizationTransactionId
            };

            var captureResponse = _paylikeTransactionService.CaptureTransaction(captureRequest);
            if(captureResponse.IsError)
            {
                result.AddError(captureResponse.ErrorMessage);
                result.AddError(captureResponse.ErrorContent);
            }
            else
            {
                result.CaptureTransactionId = captureResponse.Content.Id;
                result.NewPaymentStatus = PaymentStatus.Paid;
            }

            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            var refundRequest = new RefundTransactionRequest()
            {
                Amount = (int)(Math.Round(refundPaymentRequest.AmountToRefund, 2) * 100),
                Descriptor = _paylikePaymentSettings.RefundDescriptor,
                TransactionId = refundPaymentRequest.Order.CaptureTransactionId
            };

            var refundResponse = _paylikeTransactionService.RefundTransaction(refundRequest);
            if (refundResponse.IsError)
            {
                result.AddError(refundResponse.ErrorMessage);
                result.AddError(refundResponse.ErrorContent);
            }
            else
            {
                if(refundPaymentRequest.IsPartialRefund)
                    result.NewPaymentStatus = PaymentStatus.PartiallyRefunded;
                else
                    result.NewPaymentStatus = PaymentStatus.Refunded;
            }

            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            var voidRequest = new VoidTransactionRequest()
            {
                Amount = (int)(Math.Round(voidPaymentRequest.Order.OrderTotal, 2) * 100),
                TransactionId = voidPaymentRequest.Order.AuthorizationTransactionId
            };

            var voidResponse = _paylikeTransactionService.VoidTransaction(voidRequest);
            if (voidResponse.IsError)
            {
                result.AddError(voidResponse.ErrorMessage);
                result.AddError(voidResponse.ErrorContent);
            }
            else
            {
                result.NewPaymentStatus = PaymentStatus.Voided;
            }

            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            return result;
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            return result;
        }

        public bool CanRePostProcessPayment(Core.Domain.Orders.Order order)
        {
            return false;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "Paylike";
            routeValues = new System.Web.Routing.RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Paylike.Controllers" }, { "area", null } };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "Paylike";
            routeValues = new System.Web.Routing.RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Paylike.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaylikeController);
        }

        public bool SupportCapture => true;

        public bool SupportPartiallyRefund => true;

        public bool SupportRefund => true;

        public bool SupportVoid => true;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public override void Install()
        {
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.PublicKey", "Public key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.AppKey", "App key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.MerchantId", "Merchant Id");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.CaptureDescriptor", "Capture Descriptor");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.RefundDescriptor", "Refund Descriptor");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Fields.RedirectionTip", "You will be redirected to the Paylike website to finish your order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paylike.Errors.PaymentTokenRequired", "A payment token is required in order to continue the checkout process.");
            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaylikePaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.PublicKey");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.AppKey");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.CaptureDescriptor");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.RefundDescriptor");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Paylike.Errors.PaymentTokenRequired");
            base.Uninstall();
        }
    }
}
