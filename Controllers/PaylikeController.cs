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
using Paylike.NET.Entities;
using Paylike.NET.RequestModels.Merchants;
using Paylike.NET.RequestModels.Transactions;
using Paylike.NET.ResponseModels.Apps;
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
            paylikeSettings.CaptureDescriptor = model.CaptureDescriptor;
            paylikeSettings.RefundDescriptor = model.RefundDescriptor;
            paylikeSettings.MerchantId = GetMerchantId(paylikeSettings.AppKey, paylikeSettings.PublicKey);

            ////save settings
            _settingService.SaveSetting(paylikeSettings, x => x.PublicKey, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.AppKey, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.CaptureDescriptor, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.RefundDescriptor, storeScope);
            _settingService.SaveSetting(paylikeSettings, x => x.MerchantId, storeScope);

            //now clear settings cache
            _settingService.ClearCache();
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();

            //years
            for (int i = 0; i < 15; i++)
            {
                string year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (int i = 1; i <= 12; i++)
            {
                string text = (i < 10) ? "0" + i : i.ToString();
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = text,
                    Value = text,
                });
            }

            ViewBag.PublicKey = _paylikePaymentSettings.PublicKey;
            return View("~/Plugins/Payments.Paylike/Views/Paylike/PaymentInfo.cshtml", model);
        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            string paymentToken = form["paymenttoken"];
            if (string.IsNullOrEmpty(paymentToken))
                warnings.Add(_localizationService.GetLocaleStringResourceByName("Plugins.Payments.Paylike.Errors.PaymentTokenRequired")?.ResourceValue);
            return warnings;
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            ProcessPaymentRequest paymentInfo = new ProcessPaymentRequest();
            paymentInfo.CustomValues["paymenttoken"] = form["paymenttoken"];
            return paymentInfo;
        }

        private string GetMerchantId(string appKey, string publicKey)
        {
            try
            {
                PaylikeAppService appService = new PaylikeAppService(appKey);
                Identity app = appService.GetCurrentApp().Content.Identity;
                List<Merchant> merchants = new PaylikeMerchantService(appKey).GetMerchants(new GetMerchantsRequest()
                {
                    AppId = app.Id,
                    Limit = int.MaxValue
                }).Content;

                var configuredMerchant = merchants.FirstOrDefault(m => m.Key == publicKey);

                return configuredMerchant.Id;
            }
            catch(Exception ex)
            {
                return string.Empty;
            }
        }
    }
}
