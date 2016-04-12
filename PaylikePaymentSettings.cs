using Nop.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Paylike
{
    public class PaylikePaymentSettings : ISettings
    {
        public string MerchantId { get; set; }

        public string AppKey { get; set; }

        public string PublicKey { get; set; }

        public string CaptureDescriptor { get; set; }

        public string RefundDescriptor { get; set; }

        public bool IsValid()
        {
            bool valid = true;

            if (string.IsNullOrEmpty(MerchantId) || string.IsNullOrEmpty(AppKey) || string.IsNullOrEmpty(PublicKey) || string.IsNullOrEmpty(CaptureDescriptor)
                || string.IsNullOrEmpty(RefundDescriptor))
                valid = false;

            return valid;
        }
    }
}
