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
/// Summary description for FormatCheck
/// </summary>
public class FormatCheck
{
    /// <summary>
    /// This will extract the EAN string out of the terminal device id. If the terminal is null, a default value is returned.
    /// </summary>
    /// <param name="deviceID"></param>
    /// <returns></returns>
    internal static string GetTerminalEANString(DeviceID deviceID)
    {
        if (deviceID == null)
            return "0000000000001";

        CheckDeviceID(deviceID);
        return ((EANDeviceID)deviceID).ean;
    }

    internal static string GetEANString(DeviceID deviceID)
    {
        CheckDeviceID(deviceID);

        return ((EANDeviceID)deviceID).ean;
    }

    internal static void CheckDeviceID(DeviceID deviceID)
    {
        if (deviceID.GetType() != typeof(EANDeviceID))
            throw new GenericException(SMSVend.ErrorCodes.ERROR.DEVICEID_NOT_ALLOWED_USE_EANDEVICEID);
    }

    internal static void CheckForKWHVend(PurchaseValue purchaseValue)
    {
        if (purchaseValue.GetType() != typeof(PurchaseValueCurrency))
            throw new GenericException(SMSVend.ErrorCodes.ERROR.KWH_VEND_REQUESTS_NOT_SUPPORTED);
    }

    internal static void CheckForIDMethod(IDMethod idMethod)
    {
        if (idMethod.GetType() != typeof(VendIDMethod))
            throw new GenericException(SMSVend.ErrorCodes.ERROR.CUSTIDMETHOD_NOT_IMPLEMENTED_USE_VENDIDMETHOD);
    }

    internal static void CheckMsgID(MsgID msgID)
    {
        #region CheckDate
        if (msgID.dateTime.Length != 8 + 6)
            throw new GenericException(SMSVend.ErrorCodes.ERROR.DATETIME_FORMAT_OF_MSGID_INVALID);

        try
        {
            string DateTimeToParse = msgID.dateTime.Substring(0, 4) + "/" + msgID.dateTime.Substring(4, 2) + "/" + msgID.dateTime.Substring(6, 2) + " " + msgID.dateTime.Substring(8, 2) + ":" + msgID.dateTime.Substring(10, 2) + ":" + msgID.dateTime.Substring(12, 2);
            System.DateTime.Parse(DateTimeToParse);
        }
        catch
        {
            throw new GenericException(SMSVend.ErrorCodes.ERROR.DATETIME_FORMAT_OF_MSGID_INVALID);
        }

        if (msgID.uniqueNumber.Length > 6)
            throw new GenericException(SMSVend.ErrorCodes.ERROR.INVALID_UNIQUE_NUMBER_OF_MSGID);
        #endregion CheckDate
    }

    internal static void CheckAdviseReqType(AdviceReq adviceReq)
    {
        if (adviceReq.GetType() != typeof(LastRespAdviceReq))
            throw new GenericException(SMSVend.ErrorCodes.ERROR.ADVICEREQ_NOT_ALLOWED_USE_LASTRESPADVICEREQ);
    }

    internal static void CheckMeterSpecificEngReq(MeterSpecificEngReq meterSpecificEngReq)
    {
        if (meterSpecificEngReq.GetType() != typeof(EngKCTReq))
            throw new GenericException(SMSVend.ErrorCodes.ERROR.METERSPECIFICENGREQ_TYPE_NOT_ALLOWED);
    }

    internal static string GetMeterNumber(MeterIdentifier meterIdentifier)
    {
        if (meterIdentifier.GetType() == typeof(MeterDetail))
            return ((MeterDetail)meterIdentifier).msno;
        else if (meterIdentifier.GetType() == typeof(MeterNumber))
            return ((MeterNumber)meterIdentifier).msno;
        else
            throw new GenericException(SMSVend.ErrorCodes.ERROR.INVALID_METERIDENTIFIER);
    }
}