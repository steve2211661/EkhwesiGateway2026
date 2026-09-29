using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using SMSVMA.DLL;
using SMSVMA.BLL;
using SMSVMA.DataUtils.BOFunctions;

/// <summary>
/// Summary description for MessageLogging
/// </summary>
public class MessageLogging
{
    internal static int LogIncomingMessage(BaseReq baseReq)
    {
        return WebserviceMessageFunctions.LogIncomingMessage(baseReq, baseReq.authCred.opName, baseReq.msgID.dateTime, baseReq.msgID.uniqueNumber, HttpContext.Current.Request.UserHostAddress);
    }

    internal static void LogOutgoingMessage(BaseResp baseResp, int loggedMessageID, int? TransactionID)
    {
        WebserviceMessageFunctions.LogOutgoingMessage(baseResp, loggedMessageID, TransactionID);
    }

    internal static void LogOutgoingMessage(BaseResp baseResp, int loggedMessageID)
    {
        WebserviceMessageFunctions.LogOutgoingMessage(baseResp, loggedMessageID, null);
    }

    internal static BaseResp GetCorrespondingResponse(MsgID msgID, int userID)
    {
        return (BaseResp)WebserviceMessageFunctions.GetCorrespondingResponse(msgID.dateTime, msgID.uniqueNumber, userID);
    }
}
