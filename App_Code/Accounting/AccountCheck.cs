using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using SMSVend.ErrorCodes;

/// <summary>
/// Summary description for AccountCheck
/// </summary>
public class AccountCheck
{
    public static SMSVMA.DLL.user AuthenticateUserLINQ(AuthCred authCred)
    {
        if (authCred == null)
            throw new GenericException(SMSVend.ErrorCodes.ERROR.MERCHANT_USERNAME_PASSWORD_INCORRECT);

        SMSVMA.DLL.user currentUser = SMSVMA.DataUtils.BOFunctions.UserFunctions.AuthenticateUser(authCred.opName, authCred.password,false);
        if (currentUser != null)
            return currentUser;
        else
            throw new GenericException(SMSVend.ErrorCodes.ERROR.MERCHANT_USERNAME_PASSWORD_INCORRECT);
    }

    public static void CheckMerchantPurchase(decimal Value)
    {
        if (Value < 0)
            throw new GenericException(SMSVend.ErrorCodes.ERROR.AMOUNT_IS_UNDER_THE_MERCHANT_MINIMUM_PURCHASE_AMOUNT);

        if (Value > 5000)
            throw new GenericException(SMSVend.ErrorCodes.ERROR.AMOUNT_IS_OVER_THE_MERCHANT_MAXIMUM_PURCHASE_AMOUNT);
    }
}
