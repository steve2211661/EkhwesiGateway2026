using System;
using System.Web;
using System.Collections;
using System.Web.Services;
using System.Web.Services.Protocols;
using SMSVend.ErrorCodes;
using System.Collections.Generic;
using SMSVMA.BLL;
using FreshSystems.Utils;
using SMSVMA.DataUtils.BOFunctions;
using System.Text;
using System.Linq;
using SMSVMA.DLL;
using System.Globalization;
using System.IO;
using System.Data.SqlClient;
using System.Data;

/// <summary>
/// Summary description for EkhwesiGateway
/// </summary>
[WebService(Namespace = "http://conlog.co.za/XMLVendWebService")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
[SoapDocumentService(RoutingStyle = SoapServiceRoutingStyle.RequestElement)]
[System.Xml.Serialization.XmlInclude(typeof(ReprintDateRangeReq))]
public class EkhwesiGateway : System.Web.Services.WebService, IXMLVendServiceSoap
{

    public EkhwesiGateway()
    {
        //Uncomment the following line if using designed components 
        //InitializeComponent(); 
    }

    public static void WriteGatewayDebugFile(string Message, string clientID, string TerminalID)
    {
        try
        {
            string PPWResponseFolder = "c:\\Temp\\GatewayLogs\\" + clientID + "\\" + TerminalID;
            if (!System.IO.Directory.Exists(PPWResponseFolder))
                System.IO.Directory.CreateDirectory(PPWResponseFolder);

            string DebugFile = DateTime.Now.ToString("ddMMyyyy") + ".txt";
            if (DebugFile.Length > 0)
            {
                string path = PPWResponseFolder + '\\' + DebugFile;
                // This text is added only once to the file.
                if (!File.Exists(path))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(path))
                    {
                        sw.WriteLine("\r\n" + DateTime.Now + " " + Message);
                        sw.Close();
                    }
                }
                else
                {
                    // This text is always added, making the file longer over time
                    // if it is not deleted.
                    using (StreamWriter sw = File.AppendText(path))
                    {
                        sw.WriteLine("\r\n" + DateTime.Now + " " + Message);
                        sw.Close();
                    }
                }
            }


        }
        catch (Exception)
        {
            //Do nothing 
        }
    }

    #region IXMLVendServiceSoap Members
    [WebMethod(EnableSession = true)]
    public CreditVendResp CreditVendRequest(CreditVendReq creditVendReq)
    {
        /*
         * Check authcred for a valid username and password.
         * Check permissions on user
         * Check that meter number region matches one of the regions that the user has been assigned to
         * Perform vend
         * If Successful
         *  (PHASE 2) Add vend to current user batch
         *  Return Successful transaction to user
         * Else if valid failure
         *  (PHASE 2) Add failed transaction to current user batch with value of 0
         *  Return SoapException to user telling them what to do
         * Else
         *  Return message to user telling them to try again. We don't have to worry if a token was made on the vending server because we don't use the reports from the vending server
         */
        List<string> debugLog = new List<string>();
        string cvr = SMSVMA.VMACommon.CommonFunctions.SerializeObject(creditVendReq);

        debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- START " + "\r\n");
        debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- Incoming creditvendreq from gateway service " + "\r\n");
        debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- " + cvr + "\r\n");

        //debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 0 -- " + cvr + "\r\n");
        debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 1: MessageLogging.LogIncomingMessage method called " + "\r\n");

        int WebServiceMessageID = MessageLogging.LogIncomingMessage(creditVendReq);
        int CreditTransactionID = 0;
        int DebitTransactionID = 0;
        try
        {
            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 2: AccountCheck.AuthenticateUserLINQ method called " + "\r\n");

            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(creditVendReq.authCred);

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 3: GetUnit method called " + "\r\n");
            Resource foundResource;
            SMSVMA.DLL.unit objUnit = GetUnit(creditVendReq.idMethod, out foundResource);

            //string meterNumber = ((MeterNumber)creditVendReq.idMethod.meterIdentifier).msno;
            if (foundResource != null)
                creditVendReq.resource = foundResource;

            if (creditVendReq.resource == null)
                throw new GenericException(ERROR.RESOURCE_TYPE_NOT_FOUND);


            decimal numberOfRandsRequested;
            if (creditVendReq.purchaseValue.GetType() == typeof(PurchaseValueUnits))
            {
                throw new GenericException(ERROR.PURCHASE_TYPE_NOT_ALLOWED);
                //numberOfRandsRequested = ((PurchaseValueUnits)creditVendReq.purchaseValue).units.value * currentUnit.unitOriginalKWHRate;
            }
            else
            {
                numberOfRandsRequested = ((PurchaseValueCurrency)creditVendReq.purchaseValue).amt.value;
            }

            decimal? nAmountCredited = null;

            decimal? nThirdPartyVendingFeePercentage;
            bool ConsolidateSubAccounts = false;
            bool CanAccumulateCredit = false;
            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- Version 1.6" + "\r\n");

            nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;

            if (currentUser.userID == 99)
            {
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 4: Entered currentUser.userID == 99, easypay check " + "\r\n");

                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 5: Check thirdpartyvendingfeepercentage " + "\r\n");

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 6: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");
                //3rd party vendor collects money therefore account credit must be performed.
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                nAmountCredited = numberOfRandsRequested;
                if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                {
                    CanAccumulateCredit = true;
                }
            }
            else if (currentUser.userID == 456)
            {
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 4: Entered currentUser.userID == 99, easypay check " + "\r\n");

                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 5: Check thirdpartyvendingfeepercentage " + "\r\n");

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 6: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");
                //3rd party vendor collects money therefore account credit must be performed.
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                nAmountCredited = numberOfRandsRequested;
                if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                {
                    CanAccumulateCredit = true;
                }
            }
            else if (currentUser.userID == 121)
            {
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 8:  Entered currentUser.userID == 121 " + "\r\n");
                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                //if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                //}
                //else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.MA_Managed_EasyPay || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                //}
                //else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                //}

                if (!(objUnit.UnitTypeEnum == UnitTypes.Self_Managed && objUnit.bodycorporateregion.bodycorporate.AllowSelfManagedPPWVending))
                {
                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 9: Check for self managed " + "\r\n");
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                nThirdPartyVendingFeePercentage = 0;

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 10: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");

                //3rd party vendor collects money therefore account credit must be performed.
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 11: Check for amountcredited " + "\r\n");

                nAmountCredited = numberOfRandsRequested;

                //if (creditVendReq.clientID != null &&
                //    creditVendReq.clientID is EANDeviceID &&
                //    (creditVendReq.clientID as EANDeviceID).ean == "6001480000002")
                //{
                //    CanAccumulateCredit = true;
                //}
            }
            else if (currentUser.userID == 341)
            {
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 4: Entered currentUser.userID == 99, easypay check " + "\r\n");


                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 5: Check thirdpartyvendingfeepercentage " + "\r\n");

                nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 6: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");
                //3rd party vendor collects money therefore account credit must be performed.
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");

                nAmountCredited = numberOfRandsRequested;

                if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                {
                    CanAccumulateCredit = true;
                }
            }
            else if (currentUser.userID == 181)
            {
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 12:  Entered currentUser.userID == 181 " + "\r\n");
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 13: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");
                //3rd party vendor collects money therefore account credit must be performed.
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 14: Check for amountcredited " + "\r\n");
                nAmountCredited = numberOfRandsRequested;

                //if (creditVendReq.clientID != null && 
                //    creditVendReq.clientID is EANDeviceID &&
                //    (creditVendReq.clientID as EANDeviceID).ean == "6001480000002")
                //{
                //    CanAccumulateCredit = true;
                //}
            }
            else if (currentUser.userID == 417)
            {
                //Ekhwesi Mobile App

                if (objUnit.bodycorporateregion.bodycorporate.managingagent.maID == 34)
                {
                    //Ekhwesi #34

                    Decimal discount;

                    string test = "select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[bodycorporate] where bcID = '" + objUnit.bodycorporateregion.bodycorporate.bcID + "'";

                    DataTable dtBc = new DataTable();
                    dtBc = Globals.Globals.GetTable("select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[bodycorporate] where bcID = '" + objUnit.bodycorporateregion.bodycorporate.bcID + "'");
                    if (dtBc.Rows.Count > 0)
                    {
                        discount = Convert.ToDecimal(dtBc.Rows[0][0].ToString());
                    }
                    else
                    {
                        discount = Convert.ToDecimal(10.9);
                    }

                    decimal landlordpercentagefee = 0;
                    decimal managementpercentagefee = 0;

                    if(objUnit.ManagementPercentageFee<=0 || objUnit.ManagementPercentageFee<discount)
                    {
                        //Then deduct from landlord fee
                        landlordpercentagefee = Convert.ToDecimal(objUnit.LandlordPercentageFee - discount);

                    }
                    else
                    {
                        managementpercentagefee = Convert.ToDecimal(objUnit.ManagementPercentageFee - discount);
                    }


                    nThirdPartyVendingFeePercentage = 0;
                    Decimal NewPurchaseAmount = numberOfRandsRequested + numberOfRandsRequested / 100 * discount;

                    //3rd party vendor collects money therefore account credit must be performed.
                    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "Ekhwesi Mobile App",
                    managementpercentagefee,
                    landlordpercentagefee,
                    out CreditTransactionID);
                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                    nAmountCredited = NewPurchaseAmount;
                    if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                    {
                        CanAccumulateCredit = true;
                    }
                }
                else
                {
                    //Not Prelisha,Vargacom(Ignite)
                    if (objUnit.bodycorporateregion.bodycorporate.managingagent.maID != 96)
                    {
                        //Make token on any other MaID except Ignite 

                        //Other MA Not Ekhwesi pull ThirdPartyFee and Discount At MA level

                        Decimal discount;

                        DataTable dtBcBC = new DataTable();
                        dtBcBC = Globals.Globals.GetTable("select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[bodycorporate] where BcID = '" + objUnit.bodycorporateregion.bodycorporate.bcID + "'");
                        if (dtBcBC.Rows.Count > 0)
                        {
                            discount = Convert.ToDecimal(dtBcBC.Rows[0][0].ToString());
                        }
                        else
                        {
                            discount = Convert.ToDecimal(0.00);
                        }

                        decimal landlordpercentagefee = 0;
                        decimal managementpercentagefee = 0;

                        Decimal DiscAmt = 0;
                        Decimal finalMaFee = 0;

                        //objUnit.ManagementPercentageFee = Convert.ToDecimal(objUnit.ManagementPercentageFee - discount);

                        Decimal maDiscount = 0;

                        DataTable dtMaDiscount = new DataTable();
                        dtMaDiscount = Globals.Globals.GetTable("select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[managingagents] where maID = '" + objUnit.bodycorporateregion.bodycorporate.managingagent.maID + "'");
                        if (dtMaDiscount.Rows.Count > 0)
                        {
                            maDiscount = Convert.ToDecimal(dtMaDiscount.Rows[0][0].ToString());
                        }
                        else
                        {
                            maDiscount = Convert.ToDecimal(0.00);
                        }

                        if (objUnit.ManagementPercentageFee <= 0 || objUnit.ManagementPercentageFee < discount)
                        {
                            //Then deduct from landlord fee
                            landlordpercentagefee = Convert.ToDecimal(objUnit.LandlordPercentageFee - (objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value - maDiscount));
                            Session["landlordpercentagefee"] = landlordpercentagefee;
                        }
                        else
                        {
                            //13
                            managementpercentagefee = Convert.ToDecimal(objUnit.ManagementPercentageFee - discount);
                            Session["landlordpercentagefee"] = objUnit.LandlordPercentageFee;
                        }

                        //13-7

                        //Decimal DiscountedMaFee = managementpercentagefee - maDiscount;

                        decimal ServiceFee = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value - maDiscount;

                        finalMaFee = managementpercentagefee - ServiceFee;

                        nThirdPartyVendingFeePercentage = ServiceFee;
                        //Decimal NewPurchaseAmount = numberOfRandsRequested + numberOfRandsRequested/100* discount;

                        //landlordpercentagefee = objUnit.LandlordPercentageFee;

                        //3rd party vendor collects money therefore account credit must be performed.
                        SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                        objUnit.unitID,
                        objUnit.UnitTypeID,
                        objUnit.unitOwnerID,
                        objUnit.unitPersonID,
                        objUnit.unitMeterNumber,
                        currentUser.userID,
                        numberOfRandsRequested,
                        "Ekhwesi Mobile App",
                        finalMaFee,
                        landlordpercentagefee,
                        out CreditTransactionID);
                        debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                        nAmountCredited = numberOfRandsRequested;
                        if (creditVendReq.clientID != null &&
                        creditVendReq.clientID is EANDeviceID &&
                        ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                        || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                        {
                            CanAccumulateCredit = true;
                        }
                    }
                    else
                    {
                        //Ignite user on Ekhwesi App illegally
                        throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");

                    }
                }
            }
            else if (currentUser.userID == 420)
            {
                //Ignite User
                if (objUnit.bodycorporateregion.bodycorporate.managingagent.maID == 96)
                {
                    //Ignite

                    Decimal discount;

                    DataTable dtBc = new DataTable();
                    dtBc = Globals.Globals.GetTable("select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[managingagents] where maID = 96");
                    if (dtBc.Rows.Count > 0)
                    {
                        discount = Convert.ToDecimal(dtBc.Rows[0][0].ToString());
                    }
                    else
                    {
                        discount = Convert.ToDecimal(10.9);
                    }

                    decimal ServiceFee = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value - discount;

                    nThirdPartyVendingFeePercentage = ServiceFee;
                    //Ignite collects money therefore account credit must be performed.
                    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);
                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                    nAmountCredited = numberOfRandsRequested;
                    if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                    {
                        CanAccumulateCredit = true;
                    }
                }
                else
                {
                    //Ignite trying to buy illegally
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
            }
            else if (currentUser.userID == 517)
            {
                //Ignite User
                if (objUnit.bodycorporateregion.bodycorporate.managingagent.maID == 110)
                {
                    //Ignite

                    Decimal discount;

                    DataTable dtBc = new DataTable();
                    dtBc = Globals.Globals.GetTable("select [MobilePercentageDiscount] FROM [EKH_SmsVMA].[dbo].[managingagents] where maID = 517");
                    if (dtBc.Rows.Count > 0)
                    {
                        discount = Convert.ToDecimal(dtBc.Rows[0][0].ToString());
                    }
                    else
                    {
                        discount = Convert.ToDecimal(10.9);
                    }

                    decimal ServiceFee = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value - discount;

                    nThirdPartyVendingFeePercentage = ServiceFee;
                    //Ignite collects money therefore account credit must be performed.
                    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                    objUnit.unitID,
                    objUnit.UnitTypeID,
                    objUnit.unitOwnerID,
                    objUnit.unitPersonID,
                    objUnit.unitMeterNumber,
                    currentUser.userID,
                    numberOfRandsRequested,
                    "3rd party received payment",
                    out CreditTransactionID);
                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 7: Check for amountcredited " + "\r\n");
                    nAmountCredited = numberOfRandsRequested;
                    if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002"
                    || (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                    {
                        CanAccumulateCredit = true;
                    }
                }
                else
                {
                    //Ignite trying to buy illegally
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
            }
            else
            {
                //Webvend
                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 15: Entered else where user not 99,121,181 " + "\r\n");
                nThirdPartyVendingFeePercentage = null;

                SMSVMA.DataUtils.BOFunctions.UserFunctions.CheckUserRoleAndBodyCorpAgainstMeter(currentUser, objUnit, SMSVMA.BLL.BLLRole.UserRole.Vend);

                debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 16: Check creditVendReq.PerformAutoAccountCredit " + "\r\n");
                if (creditVendReq.PerformAutoAccountCredit)
                {
                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 17: Entered creditVendReq.PerformAutoAccountCredit statement " + "\r\n");

                    if (!currentUser.CheckUserRole(BLLRole.UserRole.Payment))
                        throw new GenericException(ERROR.EKH_INCORRECT_PERMISSIONS);

                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 18: SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit method called " + "\r\n");
                    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
                        objUnit.unitID,
                        objUnit.UnitTypeID,
                        objUnit.unitOwnerID,
                        objUnit.unitPersonID,
                        objUnit.unitMeterNumber,
                        currentUser.userID,
                        numberOfRandsRequested,
                        creditVendReq.NotesForAutoAccountCredit,
                        out CreditTransactionID);

                    debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 19: Check for amountcredited " + "\r\n");
                    nAmountCredited = numberOfRandsRequested;
                }
            }

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 20: Exited if statement for current user, created gatewaycreditvendreq " + "\r\n");
            //string strMessageForCustomer;
            //decimal numberOfRands = SMSVMA.DataUtils.BOFunctions.UnitFunctions.GetVendAmount(currentUnit, numberOfRandsRequested, out strMessageForCustomer);

            //if (numberOfRands <= 0)
            //    throw new GenericException(ERROR.EKH_INSUFFICIENT_CUSTOMER_CREDIT);//CustomerSpecificException, strMessageForCustomer);

            SMSVMA.DataUtils.Transactions.GatewayCreditVendReq objGatewayCreditVendReq = new SMSVMA.DataUtils.Transactions.GatewayCreditVendReq()
            {
                userID = currentUser.userID,
                purchaseValue = new SMSVMA.DataUtils.Transactions.PurchaseValueCurrency()
                {
                    value = numberOfRandsRequested
                },
                unitIdentifier = new SMSVMA.DataUtils.Transactions.UnitIDUnitIdentifier()
                {
                    unitID = objUnit.unitID
                },
                thirdPartyVendingFeePercentage = nThirdPartyVendingFeePercentage,
                creditTransactionID = CreditTransactionID > 0 ? (int?)CreditTransactionID : null,
                CanAccumulateCredit = CanAccumulateCredit,
                resource = GetMeterResourceType(foundResource)
            };

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 21: SMSVMA.DataUtils.Transactions.PerformCreditVendRequest created and process method called " + "\r\n");

            SMSVMA.DataUtils.Transactions.CreditVendResp objCreditVendResp;

            //MM Change made here
            if (currentUser.userID == 417)
            {
                 objCreditVendResp = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequestMobile(objGatewayCreditVendReq, nAmountCredited).Process() as SMSVMA.DataUtils.Transactions.CreditVendResp;
            }
            else
            {

                objCreditVendResp = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest(objGatewayCreditVendReq, nAmountCredited).Process() as SMSVMA.DataUtils.Transactions.CreditVendResp;
            }

            //SMSVMA.DataUtils.Transactions.CreditVendResp objCreditVendResp = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest(objGatewayCreditVendReq, nAmountCredited).Process() as SMSVMA.DataUtils.Transactions.CreditVendResp;

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 22: Assign DebitTransactionID " + "\r\n");
            DebitTransactionID = objCreditVendResp.creditVendReceipt.receiptNo;

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 23: objCreditVendResp.ConvertToGatewayCreditVendResp method called " + "\r\n");
            CreditVendResp newCreditVendResp = objCreditVendResp.ConvertToGatewayCreditVendResp(ConsolidateSubAccounts);

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 24: FillInTerminalAndServerIDs method called " + "\r\n");
            FillInTerminalAndServerIDs(newCreditVendResp, creditVendReq);

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- CreditVendRequest Point 25: MessageLogging.LogOutgoingMessage method called " + "\r\n");
            MessageLogging.LogOutgoingMessage(newCreditVendResp, WebServiceMessageID, objCreditVendResp.creditVendReceipt.receiptNo);

            debugLog.Add(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " --- END " + "\r\n");
            //logging added to check function times
            string profilLogMsg = SMSVMA.VMACommon.CommonFunctions.SerializeObject(debugLog);

            WriteGatewayDebugFile(profilLogMsg, "EKH_CLIENT", "EKH_TERMINAL" + "-log");

            return newCreditVendResp;

            //CreditVendResp creditVendResp = ProcessTokenRequest.GetCreditVendResp(meterNumber, numberOfKWH);
            //creditVendResp.creditVendReceipt.transactions.tenderAmt = new Currency() { symbol = "R", value = numberOfRands };
            //foreach (Tx tx in creditVendResp.creditVendReceipt.transactions.tx)
            //    if (tx.GetType() == typeof(CreditVendTx))
            //        ((CreditVendTx)tx).amt = new Currency()
            //        {
            //            value = ((CreditVendTx)tx).creditTokenIssue.units.value * currentUnit.unitKWHRate / 100m,
            //            symbol = "R"
            //        };
            //if (creditVendResp.respDateTime == DateTime.MinValue)
            //    creditVendResp.respDateTime = DateTime.Now;


            //creditVendResp.creditVendReceipt.receiptNo = AccountLogging.LogTransaction(creditVendResp, currentUser.userID).ToString();
            //creditVendResp.custMsg = strMessageForCustomer;
            //creditVendResp.dispHeader = BLLTransaction.TransactionTypeOptions.CreditVendRequest_Credit_Vend_203.Description();

            //MessageLogging.LogOutgoingMessage(creditVendResp, WebServiceMessageID);
            //return creditVendResp;
        }
        //catch (CustomerSpecificException ex)
        //{
        //    if (CreditTransactionID.HasValue)
        //        SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID.Value, "Account credit reversal because of failed credit vend request");

        //    EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(creditVendReq.clientID, "", creditVendReq.msgID, ex.ErrorCode, ex.Message);

        //    MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
        //    // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

        //    throw objEkhwesiVendSoapException.exSoapException;
        //}

        catch (GenericException ex)
        {
            if (CreditTransactionID > 0)
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID, "Account credit reversal because of failed credit vend request");

            if (DebitTransactionID > 0)
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(DebitTransactionID, "Account debit reversal because of failed credit vend request");

            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(creditVendReq.clientID, creditVendReq.terminalID, creditVendReq.msgID, ex);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            if (CreditTransactionID > 0)
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID, "Account credit reversal because of failed credit vend request");

            if (DebitTransactionID > 0)
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(DebitTransactionID, "Account debit reversal because of failed credit vend request");

            //  SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.Message);

            EmailSender.DLL.Send.SendExceptionEmail(null, creditVendReq.authCred.opName, "Ekhwesi Gateway", "CreditVendReq", "", HttpContext.Current.Request.UserHostAddress, ex);

            throw ex;
        }
    }

    private const string SERVERID = "";

    private void FillInTerminalAndServerIDs(BaseResp resp, BaseReq req)
    {
        resp.terminalID = req.terminalID;
        resp.serverID = new EANDeviceID()
        {
            ean = SERVERID
        };
    }


    [WebMethod(EnableSession = true)]
    public TrialCreditVendResp TrialCreditVendRequest(TrialCreditVendReq creditVendReq)
    {
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(creditVendReq);
        int? CreditTransactionID = null;
        try
        {
            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(creditVendReq.authCred);


            Resource resource;
            SMSVMA.DLL.unit objUnit = GetUnit(creditVendReq.idMethod, out resource);

            SMSVMA.DataUtils.Transactions.PurchaseValue objPurchaseValue;
            if (creditVendReq.purchaseValue is PurchaseValueUnits)
            {
                objPurchaseValue = new SMSVMA.DataUtils.Transactions.PurchaseValueUnits()
                {
                    units = ((PurchaseValueUnits)creditVendReq.purchaseValue).units.value
                };
            }
            else
            {
                objPurchaseValue = new SMSVMA.DataUtils.Transactions.PurchaseValueCurrency()
                {
                    value = ((PurchaseValueCurrency)creditVendReq.purchaseValue).amt.value
                };
            }

            bool ApplyManagementFee;
            decimal? nThirdPartyVendingFeePercentage;
            bool ConsolidateSubAccounts = false;
            bool CanAccumulateCredit = false;
            if (currentUser.userID == 99 || currentUser.userID == 417 || currentUser.userID == 416)
            {
                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;

                if (creditVendReq.clientID != null &&
                    creditVendReq.clientID is EANDeviceID &&
                    ((creditVendReq.clientID as EANDeviceID).ean == "6001480000002" ||
                    (creditVendReq.clientID as EANDeviceID).ean == "6001490000002"))
                {
                    CanAccumulateCredit = true;
                }

                ApplyManagementFee = true;
            }
            else if (currentUser.userID == 121)
            {
                ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                //if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. You may only purchase credit with Ekhwesi scratch cards");
                //}
                //else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed || objUnit.UnitTypeEnum == UnitTypes.MA_Managed_NonEasyPay)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                //}
                //else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                //{
                //    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                //}

                //nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;

                //if (creditVendReq.clientID != null &&
                //    creditVendReq.clientID is EANDeviceID &&
                //    (creditVendReq.clientID as EANDeviceID).ean == "6001480000002")
                //{
                //    CanAccumulateCredit = true;
                //}

                //ApplyManagementFee = true;


                if (!(objUnit.UnitTypeEnum == UnitTypes.Self_Managed && objUnit.bodycorporateregion.bodycorporate.AllowSelfManagedPPWVending))
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to purchase credit here. Please ask your landlord or body corporate administrator for more information");
                }

                nThirdPartyVendingFeePercentage = 0;

                ApplyManagementFee = false;
            }
            else
            {
                nThirdPartyVendingFeePercentage = null;

                SMSVMA.DataUtils.BOFunctions.UserFunctions.CheckUserRoleAndBodyCorpAgainstMeter(currentUser, objUnit, SMSVMA.BLL.BLLRole.UserRole.Vend);

                ApplyManagementFee = true;
            }



            SMSVMA.DataUtils.Transactions.GatewayTrialCreditVendReq objGatewayTrialCreditVendReq = new SMSVMA.DataUtils.Transactions.GatewayTrialCreditVendReq()
            {
                unitIdentifier = new SMSVMA.DataUtils.Transactions.UnitIDUnitIdentifier()
                {
                    unitID = objUnit.unitID
                },
                userID = currentUser.userID,
                purchaseValue = objPurchaseValue,
                thirdPartyVendingFeePercentage = nThirdPartyVendingFeePercentage.HasValue ? nThirdPartyVendingFeePercentage.Value : 0,
                CanAccumulateCredit = CanAccumulateCredit,
                ApplyManagementFee = ApplyManagementFee,
                resource = GetMeterResourceType(resource)
            };
            SMSVMA.DataUtils.Transactions.CreditVendResp objCreditVendResp = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest(objGatewayTrialCreditVendReq, null).Process() as SMSVMA.DataUtils.Transactions.CreditVendResp;



            CreditVendResp creditVendResp = objCreditVendResp.ConvertToGatewayCreditVendResp(ConsolidateSubAccounts);
            TrialCreditVendResp trialCreditVendResp = new TrialCreditVendResp()
            {
                clientID = creditVendResp.clientID,
                clientStatus = creditVendResp.clientStatus,
                creditVendReceipt = creditVendResp.creditVendReceipt,
                custMsg = creditVendResp.custMsg,
                custVendDetail = creditVendResp.custVendDetail,
                dispHeader = creditVendResp.dispHeader,
                operatorMsg = creditVendResp.operatorMsg,
                reqMsgID = creditVendResp.reqMsgID,
                respDateTime = creditVendResp.respDateTime,
                serverID = creditVendResp.serverID,
                terminalID = creditVendResp.terminalID,
                utility = creditVendResp.utility,
                vendor = creditVendResp.vendor
            };

            foreach (Tx tx in trialCreditVendResp.creditVendReceipt.transactions.tx)
                if (tx is CreditVendTx)
                    ((STS1Token)((CreditVendTx)tx).creditTokenIssue.token).stsCipher = "TRIAL";

            FillInTerminalAndServerIDs(trialCreditVendResp, creditVendReq);

            MessageLogging.LogOutgoingMessage(trialCreditVendResp, WebServiceMessageID);

            return trialCreditVendResp;
        }
        //catch (CustomerSpecificException ex)
        //{
        //    if (CreditTransactionID.HasValue)
        //        SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID.Value, "Account credit reversal because of failed credit vend request");

        //    EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(creditVendReq.clientID, "", creditVendReq.msgID, ex.ErrorCode, ex.Message);

        //    MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
        //    // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

        //    throw objEkhwesiVendSoapException.exSoapException;
        //}
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(creditVendReq.clientID, creditVendReq.terminalID, creditVendReq.msgID, ex.ErrorCode);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            if (CreditTransactionID.HasValue)
                SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID.Value, "Account credit reversal because of failed credit vend request");

            //  SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.Message);

            throw ex;
        }
    }

    [WebMethod(EnableSession = true)]
    public MeterSpecificEngResp MeterSpecificEngRequest(MeterSpecificEngReq meterSpecificEngReq)
    {
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(meterSpecificEngReq);
        try
        {
            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(meterSpecificEngReq.authCred);

            MeterSpecificEngResp meterSpecificEngResp;
            if (meterSpecificEngReq is EngKCTReq)
            {
                meterSpecificEngResp = new MeterSpecificEngFunctions(currentUser).ProcessMeterSpecificEngReq((EngKCTReq)meterSpecificEngReq);
            }
            else if (meterSpecificEngReq is PwrLmtReq)
            {
                meterSpecificEngResp = new MeterSpecificEngFunctions(currentUser).ProcessMeterSpecificEngReq((PwrLmtReq)meterSpecificEngReq);
            }
            else if (meterSpecificEngReq is ClearCreditReq)
            {
                meterSpecificEngResp = new MeterSpecificEngFunctions(currentUser).ProcessMeterSpecificEngReq((ClearCreditReq)meterSpecificEngReq);
            }
            else if (meterSpecificEngReq is ClearTamperReq)
            {
                meterSpecificEngResp = new MeterSpecificEngFunctions(currentUser).ProcessMeterSpecificEngReq((ClearTamperReq)meterSpecificEngReq);
            }
            else
            {
                throw new NotImplementedException();
            }

            FillInTerminalAndServerIDs(meterSpecificEngResp, meterSpecificEngReq);

            MessageLogging.LogOutgoingMessage(meterSpecificEngResp, WebServiceMessageID);

            return meterSpecificEngResp;
        }
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(meterSpecificEngReq.clientID, meterSpecificEngReq.terminalID, meterSpecificEngReq.msgID, ex.ErrorCode);
            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(meterSpecificEngReq.clientID, meterSpecificEngReq.terminalID, meterSpecificEngReq.msgID, ERROR.GENERAL_ERROR_RESPONSE);
            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            throw objEkhwesiVendSoapException.exSoapException;
        }
    }


    [WebMethod(EnableSession = true)]
    public AdviceResp AdviceRequest(AdviceReq adviceReq)
    {
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(adviceReq);
        try
        {
            AdviceResp adviceResp;

            if (adviceReq is LastRespAdviceReq)
            {
                SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(adviceReq.authCred);

                BaseResp objBaseResp = MessageLogging.GetCorrespondingResponse(adviceReq.adviceReqMsgID, currentUser.userID);

                if (objBaseResp == null)
                    throw new GenericException(ERROR.THIS_TRANSACTION_WAS_NOT_FOUND);

                adviceResp = new LastRespAdviceResp()
                {
                    reqMsgID = adviceReq.msgID,
                    adviceReqMsgID = adviceReq.adviceReqMsgID,
                    lastResponse = objBaseResp,
                    dispHeader = "Advice Response - Last Resp"
                };
            }
            else
            {
                throw new NotImplementedException();
            }

            FillInTerminalAndServerIDs(adviceResp, adviceReq);

            MessageLogging.LogOutgoingMessage(adviceResp, WebServiceMessageID);

            return adviceResp;
        }
        catch (CustomerSpecificException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(adviceReq.clientID, adviceReq.terminalID, adviceReq.msgID, ex.ErrorCode, ex.Message);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(adviceReq.clientID, adviceReq.terminalID, adviceReq.msgID, ex.ErrorCode);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            //  SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.Message);

            throw ex;
        }
    }

    [WebMethod(EnableSession = true)]
    public FBEResp FBERequest(FBEReq fbeReq)
    {
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(fbeReq);
        try
        {
            throw new GenericException(ERROR.NOT_ELIGIBLE_FOR_FBE);

            //FillInTerminalAndServerIDs(reprintResp, reprintReq);
        }
        catch (CustomerSpecificException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(fbeReq.clientID, fbeReq.terminalID, fbeReq.msgID, ex.ErrorCode, ex.Message);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(fbeReq.clientID, fbeReq.terminalID, fbeReq.msgID, ex.ErrorCode);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
    }

    [WebMethod(EnableSession = true)]
    public ConfirmCustomerResp ConfirmCustomerRequest(ConfirmCustomerReq confirmCustomerReq)
    {
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(confirmCustomerReq);
        try
        {
            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(confirmCustomerReq.authCred);

            Resource resource;
            SMSVMA.DLL.unit objUnit = GetUnit(confirmCustomerReq.idMethod, out resource);

            SMSVMA.DLL.ElectricityMeter objElectricityMeter = null;
            SMSVMA.DLL.ElectricityMeter objWaterMeter = null;

            if (resource == null || resource is Electricity)
            {
                if (objUnit.unitMeterNumber != null)
                    objElectricityMeter = new BLLElectricityMeter().SelectByID(objUnit.unitMeterNumber);
            }

            if (resource == null || resource is Water)
            {
                if (objUnit.WaterMeterNumber != null)
                    objWaterMeter = new BLLElectricityMeter().SelectByID(objUnit.WaterMeterNumber);
            }

            if (objUnit == null || (objElectricityMeter == null && objWaterMeter == null))
            {
                throw new GenericException(ERROR.NO_UNIT_FOUND);
            }

            SMSVMA.DLL.Transaction objLastTransaction = objUnit.Transactions.OrderByDescending(c => c.TransactionID).FirstOrDefault();
            DateTime? dteLastPurchase = objLastTransaction != null ? (DateTime?)objLastTransaction.TransactionDate : null;

            CustVendDetail objCustVendDetail;
            if (objUnit.ownerPerson != null && objUnit.ownerPerson.address != null)
            {
                StringBuilder sbAddress = new StringBuilder();
                if (!string.IsNullOrEmpty(objUnit.ownerPerson.address.addLine1))
                    sbAddress.Append(objUnit.ownerPerson.address.addLine1 + ", ");
                if (!string.IsNullOrEmpty(objUnit.ownerPerson.address.addLine2))
                    sbAddress.Append(objUnit.ownerPerson.address.addLine2 + ", ");
                if (!string.IsNullOrEmpty(objUnit.ownerPerson.address.addLine3))
                    sbAddress.Append(objUnit.ownerPerson.address.addLine3 + ", ");
                if (!string.IsNullOrEmpty(objUnit.ownerPerson.address.addLine4))
                    sbAddress.Append(objUnit.ownerPerson.address.addLine4 + ", ");
                if (!string.IsNullOrEmpty(objUnit.ownerPerson.address.addPostCode))
                    sbAddress.Append(objUnit.ownerPerson.address.addPostCode);

                objCustVendDetail = new CustVendDetail()
                {
                    accNo = objUnit.ownerPerson.perID.ToString(),
                    address = sbAddress.ToString(),
                    contactNo = objUnit.ownerPerson.perPhone,
                    name = objUnit.ownerPerson.perName + " " + objUnit.ownerPerson.perSurname,
                    daysLastPurchase = dteLastPurchase.HasValue ? dteLastPurchase.Value.ToString("dd/MM/yyyy HH:mm:ss") : string.Empty
                };
            }
            else
                objCustVendDetail = null;

            decimal nAccountBalance = UnitFunctions.GetAccountBalanceRelativeToPurchaseLimit(objUnit.unitID);

            List<ConfirmCustResult> confirmCustResult = new List<ConfirmCustResult>();
            if (objElectricityMeter != null)
            {
                decimal nMinPurchase = new SMSVMA.DataUtils.Transactions.AppliedFeeTransactions(objUnit).GetMinimumPurchase(MeterResourceTypes.Electricity);

                confirmCustResult.Add(new ConfirmCustResult()
                    {
                        meterDetail = new MeterDetail()
                        {
                            msno = objElectricityMeter.MeterNumber,
                            krn = objElectricityMeter.KRN.ToString(),
                            sgc = objElectricityMeter.SGC,
                            ti = objElectricityMeter.TariffIndex.ToString(),
                            meterType = new MeterType()
                            {
                                at = "07",
                                tt = "02"
                            }
                        },
                        custVendDetail = objCustVendDetail,
                        custVendConfig = new CustVendConfig()
                        {
                            fbeDue = false,
                            canVend = true,
                            fbeDueSpecified = false,
                            maxVendAmt = new Currency() { value = nAccountBalance },
                            minVendAmt = new Currency() { value = nMinPurchase }
                        }
                    });
            };

            if (objWaterMeter != null)
            {
                decimal nMinPurchase = new SMSVMA.DataUtils.Transactions.AppliedFeeTransactions(objUnit).GetMinimumPurchase(MeterResourceTypes.Water);

                confirmCustResult.Add(new ConfirmCustResult()
                {
                    meterDetail = new MeterDetail()
                    {
                        msno = objWaterMeter.MeterNumber,
                        krn = objWaterMeter.KRN.ToString(),
                        sgc = objWaterMeter.SGC,
                        ti = objWaterMeter.TariffIndex.ToString(),
                        meterType = new MeterType()
                        {
                            at = "07",
                            tt = "02"
                        }
                    },
                    custVendDetail = objCustVendDetail,
                    custVendConfig = new CustVendConfig()
                    {
                        fbeDue = false,
                        canVend = true,
                        fbeDueSpecified = false,
                        maxVendAmt = new Currency() { value = nAccountBalance },
                        minVendAmt = new Currency() { value = nMinPurchase }
                    }

                });
            };


            ConfirmCustomerResp confirmCustomerResp = new ConfirmCustomerResp()
            {
                reqMsgID = confirmCustomerReq.msgID,
                respDateTime = DateTime.Now,
                confirmCustResult = confirmCustResult.ToArray()

            };

            FillInTerminalAndServerIDs(confirmCustomerResp, confirmCustomerReq);

            MessageLogging.LogOutgoingMessage(confirmCustomerResp, WebServiceMessageID);
            return confirmCustomerResp;
        }
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(confirmCustomerReq.clientID, confirmCustomerReq.terminalID, confirmCustomerReq.msgID, ex.ErrorCode);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
    }

    //[WebMethod(EnableSession = true)]
    public PayAccResp PayAccountRequest(PayAccReq payAccReq)
    {
        /*
         * Check authcred for a valid username and password.
         * Check permissions on user
         * Check that meter number region matches one of the regions that the user has been assigned to
         * Perform vend
         * If Successful
         *  (PHASE 2) Add vend to current user batch
         *  Return Successful transaction to user
         * Else if valid failure
         *  (PHASE 2) Add failed transaction to current user batch with value of 0
         *  Return SoapException to user telling them what to do
         * Else
         *  Return message to user telling them to try again. We don't have to worry if a token was made on the vending server because we don't use the reports from the vending server
         */
        int WebServiceMessageID = MessageLogging.LogIncomingMessage(payAccReq);
        //int CreditTransactionID = 0;
        try
        {
            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(payAccReq.authCred);

            bool IsFixedFee = payAccReq.payAccount is ServiceChrg;

            Resource resource;
            SMSVMA.DLL.unit objUnit = GetUnit(payAccReq.payAccount.idMethod, out resource);
            SMSVMA.DataUtils.BOFunctions.UserFunctions.CheckUserRoleAndBodyCorpAgainstMeter(currentUser, objUnit, SMSVMA.BLL.BLLRole.UserRole.Vend);

            int nUnitAppliedFeeID;
            if (!int.TryParse(payAccReq.payAccount.desc, out nUnitAppliedFeeID))
            {
                throw new CustomerSpecificException(ERROR.NOT_IMPLEMENTED, "payAccount.desc was not a valid ID for UnitAppliedFee");

                //if (payAccReq.payAccount.idMethod is CustIDMethod)
                //{
                //    CustIDMethod objCustIDMethod = payAccReq.payAccount.idMethod as CustIDMethod;
                //    if (objCustIDMethod.custIdentifier is CustAccountNo)
                //    {
                //        if (!int.TryParse((objCustIDMethod.custIdentifier as CustAccountNo).accNo, out nUnitAppliedFeeID))
                //        {
                //            throw new CustomerSpecificException(ERROR.NOT_IMPLEMENTED, "CustAccountNo was not a valid ID for UnitAppliedFee");
                //        }
                //    }
                //    else
                //        throw new CustomerSpecificException(ERROR.NOT_IMPLEMENTED, "custIdentifier type not supported");
                //}
                //else
                //    throw new CustomerSpecificException(ERROR.NOT_IMPLEMENTED, "payAccount.idMethod type not supported");
            }

            SMSVMA.DLL.UnitAppliedFee objUnitAppliedFee = new BLLUnitAppliedFee().SelectByID(nUnitAppliedFeeID);
            if (objUnitAppliedFee == null)
                throw new GenericException(ERROR.NO_UNIT_FOUND);
            else if (objUnitAppliedFee.UnitID != objUnit.unitID)
                throw new CustomerSpecificException(ERROR.NO_UNIT_FOUND, "Unit Applied Fee does not match details for unit");





            decimal? nThirdPartyVendingFeePercentage;
            //bool ConsolidateSubAccounts = false;
            if (currentUser.CheckUserRole(BLLRole.UserRole.ThirdPartyVendor))
            {
                //ConsolidateSubAccounts = true;
                //3rd party vendor can only vend to MA managed meters.
                if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Scratch_Card)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to pay your account here. You may only pay your account with Ekhwesi scratch cards");
                }
                else if (objUnit.UnitTypeEnum == SMSVMA.DLL.UnitTypes.Self_Managed)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to pay your account here. Please ask your landlord or body corporate administrator for more information");
                }
                else if (!objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.HasValue)
                {
                    throw new CustomerSpecificException(ERROR.NOT_ELIGIBLE_FOR_CREDIT, "You are not able to pay your account here. Please ask your landlord or body corporate administrator for more information");
                }

                nThirdPartyVendingFeePercentage = objUnit.bodycorporateregion.bodycorporate.managingagent.ThirdPartyVendingFee.Value;
            }
            else
            {
                nThirdPartyVendingFeePercentage = null;

                SMSVMA.DataUtils.BOFunctions.UserFunctions.CheckUserRoleAndBodyCorpAgainstMeter(currentUser, objUnit, SMSVMA.BLL.BLLRole.UserRole.Vend);
            }

            //SMSVMA.DataUtils.BOFunctions.TransactionFunctions.CreditVendRequestAccountCredit(
            //            objUnit.unitID,
            //            objUnit.unitOwnerID,
            //            objUnit.unitPersonID,
            //            objUnit.unitMeterNumber,
            //            currentUser.userID,
            //            payAccReq.reqAmt.value,
            //            "Account Payment Credit",
            //            out CreditTransactionID);


            SMSVMA.DataUtils.Transactions.PayAccReq objGatewayCreditVendReq = new SMSVMA.DataUtils.Transactions.PayAccReq()
            {
                userID = currentUser.userID,
                reqAmt = payAccReq.reqAmt.value,
                unitAppliedFeeID = objUnitAppliedFee.ID,
                unitIdentifier = new SMSVMA.DataUtils.Transactions.UnitIDUnitIdentifier()
                {
                    unitID = objUnit.unitID
                },
                thirdPartyVendingFeePercentage = nThirdPartyVendingFeePercentage,
                //creditTransactionID = CreditTransactionID
            };



            SMSVMA.DataUtils.Transactions.PayAccResp objResp = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest(objGatewayCreditVendReq, payAccReq.reqAmt.value).Process() as SMSVMA.DataUtils.Transactions.PayAccResp;


            PayAccResp payAccResp = objResp.ConvertToGatewayCreditVendResp();

            MessageLogging.LogOutgoingMessage(payAccResp, WebServiceMessageID, objResp.payAccReceipt.receiptNo);

            return payAccResp;
        }
        //catch (CustomerSpecificException ex)
        //{
        //    if (CreditTransactionID.HasValue)
        //        SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID.Value, "Account credit reversal because of failed credit vend request");

        //    EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(creditVendReq.clientID, "", creditVendReq.msgID, ex.ErrorCode, ex.Message);

        //    MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
        //    // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

        //    throw objEkhwesiVendSoapException.exSoapException;
        //}
        catch (GenericException ex)
        {
            //if (CreditTransactionID > 0)
            //    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID, "Account credit reversal because of failed credit vend request");

            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(payAccReq.clientID, payAccReq.terminalID, payAccReq.msgID, ex);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            //if (CreditTransactionID > 0)
            //    SMSVMA.DataUtils.BOFunctions.TransactionFunctions.ReverseTransaction(CreditTransactionID, "Account credit reversal because of failed credit vend request");

            //  SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.Message);

            throw ex;
        }
    }

    private MeterResourceTypes GetMeterResourceType(Resource resource)
    {
        if (resource is Electricity)
            return MeterResourceTypes.Electricity;
        else if (resource is Water)
            return MeterResourceTypes.Water;
        else
            throw new NotImplementedException(resource.GetType().ToString());
    }

    private SMSVMA.DLL.unit GetUnit(IDMethod iDMethod, out Resource resource)
    {
        if (iDMethod is CustIDMethod)
        {
            CustIDMethod custIDMethod = iDMethod as CustIDMethod;
            if (custIDMethod.custIdentifier is CustAccountNo)
            {
                CustAccountNo custAccountNo = custIDMethod.custIdentifier as CustAccountNo;

                int UnitID;
                if (int.TryParse(custAccountNo.accNo, out UnitID))
                {
                    SMSVMA.DLL.unit objUnit = new BLLUnit(null).SelectByID(UnitID);
                    if (objUnit != null)
                    {
                        resource = null;
                        return objUnit;
                    }
                    else
                        throw new GenericException(ERROR.NO_UNIT_FOUND);
                }
                else
                    throw new GenericException(ERROR.NOT_IMPLEMENTED);
            }
            else
                throw new GenericException(ERROR.NOT_IMPLEMENTED);
        }
        else if (iDMethod is VendIDMethod)
        {
            VendIDMethod vendIDMethod = iDMethod as VendIDMethod;

            string msno;

            if (vendIDMethod.meterIdentifier is MeterNumber)
            {
                MeterNumber meterNumber = vendIDMethod.meterIdentifier as MeterNumber;
                msno = meterNumber.msno;
            }
            else if (vendIDMethod.meterIdentifier is MeterCard)
            {
                MeterCard meterCard = vendIDMethod.meterIdentifier as MeterCard;

                if (meterCard.track2Data == null || meterCard.track2Data.Length < (17))
                {
                    msno = meterCard.track2Data.Substring(6, 11);
                }
                else
                    throw new GenericException(ERROR.INVALID_METER_CARD_DATA);
            }
            else
                throw new GenericException(ERROR.NOT_IMPLEMENTED);

            if (!msno.IsValidMeterNumber())
                throw new GenericException(ERROR.INVALID_METER_NUMBER);

            SMSVMA.DLL.unit objUnit = new BLLUnit(null).SelectByElectricityMeterNumber(msno);
            if (objUnit != null)
            {
                resource = new Electricity();
                return objUnit;
            }
            else
            {
                objUnit = new BLLUnit(null).SelectByWaterMeterNumber(msno);
                if (objUnit != null)
                {
                    resource = new Water();
                    return objUnit;
                }
                else
                    throw new GenericException(ERROR.NO_UNIT_FOUND);
            }
        }

        else
            throw new GenericException(ERROR.NOT_IMPLEMENTED);
    }

    [WebMethod(EnableSession = true)]
    public ReprintResp ReprintRequest(ReprintReq reprintReq)
    {
        var conn = System.Configuration.ConfigurationManager.ConnectionStrings["SMSVMA.DLL.Properties.Settings.SMSVMAConnectionString"];

        int WebServiceMessageID = MessageLogging.LogIncomingMessage(reprintReq);
        try
        {
            var myConn = new SqlConnection(conn.ConnectionString);

            myConn.Open();

            SMSVMA.DLL.user currentUser = AccountCheck.AuthenticateUserLINQ(reprintReq.authCred);

            unit objUnit;
            if (reprintReq.idMethod is AccountVendIDMethod)
            {
                int nUnitID = int.Parse(((AccountVendIDMethod)reprintReq.idMethod).accountNo);
                objUnit = new BLLUnit(null).SelectByID(nUnitID);
            }
            else if (reprintReq.idMethod is VendIDMethod)
            {
                string meterNumber = ((MeterNumber)((VendIDMethod)reprintReq.idMethod).meterIdentifier).msno;

                objUnit = new BLLUnit(null).SelectByAnyMeterNumber(meterNumber);
            }
            else
                throw new Exception("Invalid IDMethod");

            SMSVMA.DataUtils.BOFunctions.UserFunctions.CheckUserRoleAndBodyCorpAgainstMeter(currentUser, objUnit, SMSVMA.BLL.BLLRole.UserRole.None);

            if (!reprintReq.pageNumber.HasValue) reprintReq.pageNumber = 0;
            if (!reprintReq.pageSize.HasValue || reprintReq.pageSize.Value == 0) reprintReq.pageSize = 5;
            if (reprintReq.dateFrom.HasValue && reprintReq.dateFrom.Value == DateTime.MinValue) reprintReq.dateFrom = null;
            if (reprintReq.dateTo.HasValue && reprintReq.dateTo.Value == DateTime.MinValue) reprintReq.dateTo = null;

            //VendIDMethod vendIDMethod = (VendIDMethod)reprintReq.idMethod;
            //if (vendIDMethod.meterIdentifier.GetType() == typeof(MeterNumber))
            //{
            int NumPages;
            BaseVendResp[] lstBaseVendResp = SMSVMAFunctions.GetTransactions(objUnit.unitID, reprintReq.dateFrom, reprintReq.dateTo, reprintReq.pageNumber.Value, reprintReq.pageSize.Value, out NumPages);

            ReprintResp reprintResp = new ReprintResp()
            {
                reprint = lstBaseVendResp,
                numberOfPages = NumPages
            };

            FillInTerminalAndServerIDs(reprintResp, reprintReq);

            MessageLogging.LogOutgoingMessage(reprintResp, WebServiceMessageID);
            return reprintResp;
            //}
            //else
            //{
            //    throw new Exception("Invalid MeterIdentifier");
            //}
        }
        catch (CustomerSpecificException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(reprintReq.clientID, reprintReq.terminalID, reprintReq.msgID, ex.ErrorCode, ex.Message);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (GenericException ex)
        {
            EkhwesiVendSoapException objEkhwesiVendSoapException = new EkhwesiVendSoapException(reprintReq.clientID, reprintReq.terminalID, reprintReq.msgID, ex.ErrorCode);

            MessageLogging.LogOutgoingMessage(objEkhwesiVendSoapException.objXMLVendFaultResp, WebServiceMessageID);
            // SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.ErrorCode + "    -    " + ex.ErrorDescription);

            throw objEkhwesiVendSoapException.exSoapException;
        }
        catch (Exception ex)
        {
            //  SMSVend.Common.CommonFunctions.EmailDeveloper("Gateway email - Service Exception", "MessageID: " + creditVendReq.msgID.dateTime + " " + creditVendReq.msgID.uniqueNumber + " \r\nMerchantID: " + ((EANDeviceID)creditVendReq.clientID).ean + " \r\nTerminalID: " + TerminalID + " \r\nException: " + ex.GetType().FullName + "    -    " + ex.Message);

            throw ex;
        }
    }


    #region Not used



    //[WebMethod(EnableSession = true)]
    public CancelVendResp CancelVendRequest(CancelVendReq cancelVendReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public ConfirmMeterResp ConfirmMeterRequest(ConfirmMeterReq confirmMeterReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public MeterCreditTransferResp MeterCreditTransferRequest(MeterCreditTransferReq meterCreditTransferReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public VerifyResp VerifyRequest(VerifyReq verifyReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    // [WebMethod(EnableSession = true)]
    public FreeIssueResp FreeIssueRequest(FreeIssueReq freeIssueReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public CustReportFaultResp CustReportFaultRequest(CustReportFaultReq custReportFaultReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public StartBatchResp StartBatchRequest(StartBatchReq startBatchReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public EndBatchResp EndBatchRequest(EndBatchReq endBatchReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public TotalBatchResp TotalBatchRequest(TotalBatchReq totalBatchReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public UpdateMeterKeyResp UpdateMeterKeyRequest(UpdateMeterKeyReq updateMeterKeyReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public VendorStatementResp VendorStatementRequest(VendorStatementReq vendorStatementReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public DepositResp DepositSlipRequest(DepositReq depositReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public ReprintDepositResp ReprintDepositSlipRequest(ReprintDepositReq reprintDepReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public ReprintEndBatchResp ReprintEndBatchRequest(ReprintEndBatchReq reprintEndBatchReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }

    //[WebMethod(EnableSession = true)]
    public NonMeterSpecificEngResp NonMeterSpecificEngRequest(NonMeterSpecificEngReq nonMeterSpecificEngReq)
    {
        throw new Exception("The method or operation is not implemented.");
    }
    #endregion Not used
    #endregion
}

[Serializable]
[System.Xml.Serialization.XmlType(Namespace = "http://www.nrs.eskom.co.za/xmlvend/revenue/2.1/schema")]
[System.CodeDom.Compiler.GeneratedCode("wsdl", "2.0.50727.42")]
[System.Diagnostics.DebuggerStepThrough]
[System.ComponentModel.DesignerCategory("code")]
public class ReprintDateRangeReq : ReprintReq
{
    public DateTime fromDate { get; set; }
    public DateTime toDate { get; set; }

    public ReprintDateRangeReq()
        : base()
    {

    }
}