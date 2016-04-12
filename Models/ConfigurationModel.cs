using Nop.Web.Framework;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.Paylike.Models
{
    public class ConfigurationModel
    {
        [Required]
        [NopResourceDisplayName("Plugins.Payments.Paylike.Fields.MerchantId")]
        public string MerchantId { get; set; }

        [Required]
        [NopResourceDisplayName("Plugins.Payments.Paylike.Fields.AppKey")]
        public string AppKey { get; set; }

        [Required]
        [NopResourceDisplayName("Plugins.Payments.Paylike.Fields.PublicKey")]
        public string PublicKey { get; set; }

        [Required]
        [NopResourceDisplayName("Plugins.Payments.Paylike.Fields.CaptureDescriptor")]
        public string CaptureDescriptor { get; set; }

        [Required]
        [NopResourceDisplayName("Plugins.Payments.Paylike.Fields.RefundDescriptor")]
        public string RefundDescriptor { get; set; }
    }
}
