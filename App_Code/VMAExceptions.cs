using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;

/// <summary>
/// Summary description for VMAExceptions
/// </summary>
namespace VMAExceptions
{
    public class ProcessNewMessageException : Exception
    {
        public ProcessNewMessageException(string Message)
            : base(Message)
        {

        }
    }

    public class ProcessCommandException : Exception
    {
        public ProcessCommandException(string Message)
            : base(Message)
        {

        }
    }
}

namespace VMAErrorMessages
{
    public class ProcessNewMessageErrorMessages
    {
        public static string SMSVendErrorMessage
        {
            get { return "There is a problem with your message. Please use (METERNO)*(REGION)*FREE"; }
        }

        public static string PleaseUseFullFormat
        {
            get { return "Please use M(PIN)*(METERNO)*(RANDVALUE)"; }
        }

        public static string PleaseContactHelpdesk
        {
            get { return "Please contact the helpdesk on 0861119990."; }
        }

        public static string TenantNotActive
        {
            get { return "The tenant is not active for this particular unit. Please use the owner's pin number"; }
        }

        public static string CellNotRegistered
        {
            get { return "The cellphone number used is not registered in the VMA system. " + PleaseUseFullFormat; }
        }

        public static string CellNumberNotATenant
        {
            get { return "The cellphone you have used is not a tenant's cellphone. " + PleaseUseFullFormat; }
        }

        public static string CellNumberRegisteredAsManyTenants
        {
            get { return "You are registered as a tenant for more than 1 unit." + PleaseUseFullFormat; }
        }

        public static string InvalidMeterNumber
        {
            get { return "The meter number you have used is invalid."; }
        }

        public static string MeterNumberNotRegistered
        {
            get { return "Your meter number is not registered on our system." + PleaseContactHelpdesk; }
        }

        public static string CellNumberNotTheOwner
        {
            get { return "The cellphone you have used is not the owner's cellphone." + PleaseUseFullFormat; }
        }

        public static string PinIncorrect
        {
            get { return "Your pin is incorrect. Please resend your command using your correct 4 digit pin."; }
        }

        public static string IncorrectMessage
        {
            get { return "There was a problem with your message. " + PleaseUseFullFormat; }
        }
    }

    public class ProcessCommandErrorMessages
    {
        public static string CommandOnlyAvailableToOwner
        {
            get { return "This command is only available to the landlord. Please use the landlord's pin for this command."; }
        }

        public static string NoTenantAccount
        {
            get { return "Your tenant account has not been activated."; }
        }

        public static string AmountToVendMustBeGreaterThanZero
        {
            get { return "The amount to vend must be greater than zero."; }
        }

        public static string AlreadyInTempQueue
        {
            get { return "We are aware of your token request which will be processed as soon as possible."; }
        }

        public static string NoFreeElectricityAllowed
        {
            get { return "Your body corporate does not allow free electricity."; }
        }
    }
}
