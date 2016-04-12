using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Paylike.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Paylike.NET;
using Paylike.NET.RequestModels.Transactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.Paylike.Controllers
{
    public class PaylikeController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly PaylikePaymentSettings _paylikePaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;

        public PaylikeController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            PaylikePaymentSettings paylikePaymentSettings,
            ICurrencyService currencyService,
            CurrencySettings currencySettings,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._paylikePaymentSettings = paylikePaymentSettings;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var paylikeSettings = _settingService.LoadSetting<PaylikePaymentSettings>(storeScope);

            var model = new ConfigurationModel();

            model.PublicKey = paylikeSettings.PublicKey;
            model.AppKey = paylikeSettings.AppKey;
            model.MerchantId = paylikeSettings.MerchantId;
            model.CaptureDescriptor = paylikeSettings.CaptureDescriptor;
            model.RefundDescriptor = paylikeSettings.RefundDescriptor;

            return View("~/Plugins/Payments.Paylike/Views/Paylike/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var paylikeSettings = _settingService.LoadSetting<PaylikePaymentSettings>(storeScope);

            paylikeSettings.PublicKey = model.PublicKey;
            paylikeSettings.AppKey = model.AppKey;
            paylikeSettings.MerchantId = model.MerchantId;
            paylikeSettings.CaptureDescriptor = model.CaptureDescriptor;
            paylikeSettings.RefundDescriptor = model.RefundDescriptor;

            ////save settings
            _settingService.SaveSetting(paylikeSettings, x => x.PublicKey, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.AppKey, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.MerchantId, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.CaptureDescriptor, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.RefundDescriptor, storeScope);

            //now clear settings cache
            _settingService.ClearCache();
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Paylike/Views/Paylike/PaymentInfo.cshtml");
        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            ProcessPaymentRequest paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        public ActionResult FinishOrder(string transactionId)
        {
            var paylikeTransactionService = new PaylikeTransactionService(_paylikePaymentSettings.AppKey);
            var response = paylikeTransactionService.GetTransaction(new GetTransactionRequest() { TransactionId = transactionId });

            int orderId = int.Parse(response.Content.Custom["reference"]);
            Order order = _orderService.GetOrderById(orderId);

            order.AuthorizationTransactionId = transactionId;
            _orderProcessingService.MarkAsAuthorized(order);

            return RedirectToRoute("CheckoutCompleted", new { orderId = orderId });
        }
    }
}
