using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Xml.Serialization;
using System.Text;
using System.IO;
using System.Xml;
using SMSVend.ErrorCodes;

/// <summary>
/// Summary description for AccountLogging
/// </summary>
public class AccountLogging
{
    //public static int LogTransactionbfd(BaseVendResp baseVendResp, int UserID)
    //{
    //    decimal amount;
    //    decimal? units;
    //    string receiptNumber;
    //    decimal? taxAmount;
    //    decimal? rounding;
    //    decimal? change;
    //    DateTime? transactionDateTime;
    //    string stsTokenData;
    //    string msgIDDateTime;
    //    string msgIDUniqueNumber;
    //    string merchantID;
    //    string terminalID;
    //    string meterSerialNumber;
    //    decimal? availableCredit;
    //    int transactionTypeID;
    //    string regionCode;
    //    string cduID;

    //    try
    //    {
    //        if (baseVendResp.GetType() == typeof(CreditVendResp))
    //        {
    //            CreditVendResp creditVendResp = (CreditVendResp)baseVendResp;
    //            transactionDateTime = creditVendResp.respDateTime;

    //            CreditVendTx creditVendTx = (CreditVendTx)creditVendResp.creditVendReceipt.transactions.tx[0];
    //            meterSerialNumber = creditVendTx.creditTokenIssue.meterDetail.msno;

    //            units = creditVendTx.creditTokenIssue.units.value;
    //            decimal nTransactionAmt = creditVendTx.amt.value;

    //            decimal KWHRateUsed = Math.Round((creditVendTx.amt.value / creditVendTx.creditTokenIssue.units.value * 100m), 2);

    //            SMSVMA.DLL.SMSVMAqueue queue = new SMSVMA.DLL.SMSVMAqueue()
    //            {
    //                queueActivationCode = ((STS1Token)creditVendTx.creditTokenIssue.token).stsCipher,
    //                queueAttempts = 0,
    //                queueCellNumber = "",
    //                queueDateTimeAdded = creditVendResp.respDateTime,
    //                queueDateTimeSent = DateTime.Now,
    //                queueFreeMessage = false,
    //                queueIsProcessed = true,
    //                queueIsRetry =false,
    //                queueIsSent = true,
    //                queueKWHRateUsed = KWHRateUsed,
    //                queueMessageIn = "CreditVendReq",
    //                queueMessageOut = "CreditVendResp",
    //                queueMeterNumber = meterSerialNumber,
    //                queueRandValue = creditVendTx.creditTokenIssue.units.value,
    //                queueTimeAttempted = DateTime.Now,
    //                queueUnsent = false,
    //                queueUnsentAttempts = 0
    //            };

    //            new SMSVMA.BLL.BLLQueue().Insert(queue);



    //            SMSVMA.DLL.unit objUnit = SMSVMA.DataUtils.BOFunctions.UnitFunctions.IdentifyUnitLINQ(meterSerialNumber);

    //            //int TransactionID;
    //            //SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequest(
    //            //    objUnit.unitID, 
    //            //    objUnit.unitOwnerID, 
    //            //    objUnit.unitPersonID, 
    //            //    objUnit.unitMeterNumber, 
    //            //    UserID, 
    //            //    nTransactionAmt, 
    //            //    queue.queueID, 
    //            //    out TransactionID);

    //            //return TransactionID;
    //            return 0;
    //        }
    //        else
    //        {
    //            return 0;
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        throw new Exception("Problem extracting data from transaction", ex);
    //    }
    //}
}


                //if (Response.OriginalRequest.RequestType == SMSVend.RequestQueue.REQUESTTYPE.VEND ||
                //    Response.OriginalRequest.RequestType == SMSVend.RequestQueue.REQUESTTYPE.REPRINT_MISSING_TRANSACTION)
                //{
                //    amount = Response.OriginalRequest.Amount.Value;
                //    units = Response.vendDetails.Units;
                //    taxAmount = Response.vendDetails.VAT;
                //    stsTokenData = Response.vendDetails.Token;
                //    meterSerialNumber = Response.meterDetails.MeterNumber;
                //    receiptNumber = Response.vendDetails.ReceiptNo;
                //    rounding = 0;
                //    change = 0;
                //    transactionDateTime = DateTime.Now;
                //    msgIDDateTime = Response.OriginalRequest.messageIdentifier.dateTime;
                //    msgIDUniqueNumber = Response.OriginalRequest.messageIdentifier.uniqueNo;
                //    merchantID = Response.OriginalRequest.messageIdentifier.merchantID;
                //    terminalID = Response.OriginalRequest.messageIdentifier.terminalID;
                //    availableCredit = AccountLogging.GetClientStatus(Response.OriginalRequest.messageIdentifier.merchantID).availCredit.value;
                //    regionCode = Response.meterDetails.RegionCode;
                //    cduID = Response.meterDetails.CDUID;
                //    transactionTypeID = 1;  //Credit vend request
                //}
                //else if (Response.OriginalRequest.RequestType == SMSVend.RequestQueue.REQUESTTYPE.FREE)
                //{
                //    amount = 0;
                //    units = Response.vendDetails.Units;
                //    taxAmount = Response.vendDetails.VAT;
                //    stsTokenData = Response.vendDetails.Token;
                //    meterSerialNumber = Response.meterDetails.MeterNumber;
                //    receiptNumber = Response.vendDetails.ReceiptNo;
                //    rounding = 0;
                //    change = 0;
                //    transactionDateTime = DateTime.Now;
                //    msgIDDateTime = Response.OriginalRequest.messageIdentifier.dateTime;
                //    msgIDUniqueNumber = Response.OriginalRequest.messageIdentifier.uniqueNo;
                //    merchantID = Response.OriginalRequest.messageIdentifier.merchantID;
                //    terminalID = Response.OriginalRequest.messageIdentifier.terminalID;
                //    availableCredit = AccountLogging.GetClientStatus(Response.OriginalRequest.messageIdentifier.merchantID).availCredit.value;
                //    regionCode = Response.meterDetails.RegionCode;
                //    cduID = Response.meterDetails.CDUID;
                //    transactionTypeID = 2;  //FBE
                //}
                //else
                //{
                //    amount = 0;
                //    units = 0;
                //    receiptNumber = "";
                //    taxAmount = 0;
                //    rounding = 0;
                //    change = 0;
                //    transactionDateTime = DateTime.Now;
                //    stsTokenData = "";
                //    msgIDDateTime = "";
                //    msgIDUniqueNumber = "";
                //    merchantID = "";
                //    terminalID = "";
                //    meterSerialNumber = "";
                //    availableCredit = 0;
                //    regionCode = "";
                //    cduID = ""; 
                //    transactionTypeID = 2;
                //}

                //SMSVMAqueue smsvmaQueue = SMSVMAqueue.CreateSMSVMAqueue(
                // CellNumber,

            

            //    try
            //    {
            //        SMSVendGateway.Entities.Transactions transaction = SMSVendGateway.Entities.Transactions.CreateTransactions(
            //            amount,
            //            units,
            //            receiptNumber,
            //            taxAmount,
            //            rounding,
            //            change,
            //            transactionDateTime,
            //            stsTokenData,
            //            msgIDDateTime,
            //            msgIDUniqueNumber,
            //            merchantID,
            //            terminalID,
            //            meterSerialNumber,
            //            availableCredit,
            //            transactionTypeID,
            //            cduID,
            //            regionCode);

            //        DataRepository.TransactionsProvider.Insert(transaction);
            //    }
            //    catch (Exception ex)
            //    {
            //        throw new Exception("Problem saving transaction", ex);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    string Info = "";
            //    try
            //    {
            //        Info = "MsgID dt un:" + Response.OriginalRequest.messageIdentifier.dateTime + " " + Response.OriginalRequest.messageIdentifier.uniqueNo + " \r\nMerchantID: " + Response.OriginalRequest.messageIdentifier.merchantID + " \r\nTerminalID: " + Response.OriginalRequest.messageIdentifier.terminalID;
            //    }
        
        //    SMSVend.Common.CommonFunctions.EmailDeveloper("LogTransaction()", "Info: "+Info + " \r\n"+SMSVend.Common.CommonFunctions.ExtractAllErrors(ex));
        //}



        //    public static void LogOutgoingVendRequest(object VendResp, string MessageIDDateTime, string MessageIDUniqueNo, string MerchantID, string TerminalID)
        //    {
        //        MerchantRequests CurrentMerchantRequest = DataRepository.MerchantRequestsProvider.GetByMsgIDDateTimeMsgIDUniqueNumberMerchantIDTerminalID(MessageIDDateTime, MessageIDUniqueNo, MerchantID, TerminalID);

        //        CurrentMerchantRequest.Response = SMSVend.Common.CommonFunctions.SerializeObject(VendResp);

        //        DataRepository.MerchantRequestsProvider.Save(CurrentMerchantRequest);
        //    }

        //    public static object RetrieveMerchantRequest(MsgID msgID, string MerchantID, string TerminalID)
        //    {
        //        MerchantRequests CurrentMerchantRequest = DataRepository.MerchantRequestsProvider.GetByMsgIDDateTimeMsgIDUniqueNumberMerchantIDTerminalID(msgID.dateTime, msgID.uniqueNumber, MerchantID, TerminalID);

        //        if (CurrentMerchantRequest == null)
        //            throw new GenericException(SMSVend.ErrorCodes.ERROR.THIS_TRANSACTION_WAS_NOT_FOUND);

        //        if (CurrentMerchantRequest.Response == null || CurrentMerchantRequest.Response == "")
        //            return null;
        ////            throw new GenericException(SMSVend.ErrorCodes.ERROR.THIS_TRANSACTION_IS_STILL_PENDING);

        //        object VendResp;
        //        try
        //        {
        //            VendResp = SMSVend.Common.CommonFunctions.DeserializeObject(CurrentMerchantRequest.Response, SMSVend.Common.CommonFunctions.GetTypeOfXMLString(CurrentMerchantRequest.Response));
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new GenericException(SMSVend.ErrorCodes.ERROR.ERROR_DESERIALIZING_REQUEST);
        //        }

        //        return VendResp;
        //    }



        //    internal static void BeginTransaction(Merchants CurrentMerchant, decimal Value)
        //    {
        //        try
        //        {
        //            //The ReserveFunds StoredProcedure will throw an exception if the merchant does not have enough credit to reserve.
        //            DataRepository.MerchantsProvider.ReserveFunds(CurrentMerchant.MerchantID, Value);
        //        }
        //        catch
        //        {
        //            throw new GenericException(SMSVend.ErrorCodes.ERROR.MERCHANT_DOES_NOT_HAVE_SUFFICIENT_CREDIT_FOR_PURCHASE);
        //        }

        //        CurrentMerchant = DataRepository.MerchantsProvider.GetByMerchantID(CurrentMerchant.MerchantID);
        //    }

        //    internal static void CommitTransaction(string MerchantID, decimal Value)
        //    {
        //        try
        //        {
        //            DataRepository.MerchantsProvider.CommitTransaction(MerchantID, Value);
        //        }
        //        catch (Exception ex)
        //        {
        //            SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway error", "Error Committing transaction \r\n" + ex.Message +" \r\nMerchantID: "+MerchantID+" \r\nValue: "+Value);
        //            throw ex;
        //        }
        //    }

        //    internal static void ReverseTransaction(string MerchantID, decimal Value)
        //    {
        //        try
        //        {
        //            DataRepository.MerchantsProvider.ReverseTransaction(MerchantID, Value);
        //        }
        //        catch (Exception ex)
        //        {
        //            SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway error", "Error Reversing transaction \r\n" + ex.Message + " \r\nMerchantID: " + MerchantID + " \r\nValue: " + Value);
        //            throw ex;
        //        }
        //    }

        //    internal static ClientStatus GetClientStatus(string clientID)
        //    {
        //        decimal? creditLimitLessReservedFunds = null;
        //        DataRepository.MerchantsProvider.GetMerchantCreditLimitLessReservedFunds(clientID, ref creditLimitLessReservedFunds);
        //        ClientStatus CS = new ClientStatus();
        //        CS.availCredit = new Currency();
        //        CS.availCredit.symbol = Common.CurrencySymbol;
        //        CS.availCredit.value = creditLimitLessReservedFunds.Value;
        //        return CS;
        //    }


    
