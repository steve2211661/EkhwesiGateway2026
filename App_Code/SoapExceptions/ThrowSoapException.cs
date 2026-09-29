using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Web.Services.Protocols;
//using System.Runtime.Remoting.Contexts;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using SMSVend.ErrorCodes;
using SMSVMA.VMACommon;

/// <summary>
/// Summary description for ThrowSoapException
/// </summary>
public class EkhwesiVendSoapException
{
    public XMLVendFaultResp objXMLVendFaultResp { get; private set; }
    public SoapException exSoapException { get; private set; }

    public EkhwesiVendSoapException(DeviceID ClientID, DeviceID TerminalID, MsgID msgID, GenericException ex)
    {
        string CustomerMessage;
        string OperatorMessage;

        if (ex is CustomerSpecificException)
        {
            CustomerMessage = OperatorMessage = ex.Message;
        }
        else
        {
            
            ErrorAttributes.GetCustomerOperatorMessages(ex.ErrorCode, out CustomerMessage, out OperatorMessage);
        }

        GenerateSoapException(ClientID, TerminalID, msgID, "ErrorCode " + ((int)ex.ErrorCode), ErrorAttributes.GetErrorString(ex.ErrorCode), OperatorMessage, CustomerMessage, ex.ErrorCode);
    }

    public EkhwesiVendSoapException(DeviceID ClientID, DeviceID TerminalID, MsgID msgID, SMSVend.ErrorCodes.ERROR ErrorType)
    {
        string CustomerMessage;
        string OperatorMessage;
        ErrorAttributes.GetCustomerOperatorMessages(ErrorType, out CustomerMessage, out OperatorMessage);

        GenerateSoapException(ClientID, TerminalID, msgID, "ErrorCode " + ((int)ErrorType), ErrorAttributes.GetErrorString(ErrorType), OperatorMessage, CustomerMessage, ErrorType);
    }

    //public EkhwesiVendSoapException(DeviceID ClientID, string TerminalID, MsgID msgID, string DisplayHeader, string FaultDescription, string OperatorMessage, string CustomerMessage)
    //{
    //    GenerateSoapException(ClientID, TerminalID, msgID, DisplayHeader, FaultDescription, OperatorMessage, CustomerMessage);
    //}

    public EkhwesiVendSoapException(DeviceID ClientID, DeviceID TerminalID, MsgID msgID, SMSVend.ErrorCodes.ERROR ErrorType, string CustomerSpecificMessage)
    {
        string CustomerMessage;
        string OperatorMessage;
        ErrorAttributes.GetCustomerOperatorMessages(ErrorType, out CustomerMessage, out OperatorMessage);

        GenerateSoapException(ClientID, TerminalID, msgID, "ErrorCode " + ((int)ErrorType), ErrorAttributes.GetErrorString(ErrorType), OperatorMessage, CustomerSpecificMessage, ErrorType);
    }

    public void GenerateSoapException(DeviceID ClientID, DeviceID TerminalID, MsgID msgID, string DisplayHeader, string FaultDescription, string OperatorMessage, string CustomerMessage, ERROR error)
    {
        Fault fault = GetFaultFromErrorCode(error);
        fault.desc = FaultDescription;

        objXMLVendFaultResp = new XMLVendFaultResp()
        {
            clientID = ClientID,
            custMsg = CustomerMessage,
            dispHeader = DisplayHeader,
            operatorMsg = OperatorMessage,
            reqMsgID = msgID,
            respDateTime = DateTime.Now,
            terminalID = TerminalID,
            fault = fault,
            serverID = new EANDeviceID()
            {
                ean = ""
            }
        };

        XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces();
        xmlns.Add("", "");
        xmlns.Add("b0", "http://www.nrs.eskom.co.za/xmlvend/base/2.1/schema");
        xmlns.Add("eb", "http://www.eskom.co.za/xmlvend/base-ext/2.1.1/schema");
        xmlns.Add("r0", "http://www.nrs.eskom.co.za/xmlvend/revenue/2.1/schema");
        xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

        string XMLisedString = CommonFunctions.SerializeObject(objXMLVendFaultResp, xmlns);

        XmlDocument xmldoc = new XmlDocument();
        xmldoc.LoadXml(XMLisedString);

        System.Xml.XmlNode node = xmldoc.CreateNode(XmlNodeType.Element, SoapException.DetailElementName.Name, SoapException.DetailElementName.Namespace);
        node.AppendChild(xmldoc.FirstChild.NextSibling);

        exSoapException = new SoapException("Fault occurred", SoapException.ServerFaultCode, "", node);
    }

    private static Fault GetFaultFromErrorCode(ERROR ErrorCode)
    {
        Fault fault;
        //if (ErrorAttributes.GetFaultTypeAttribute(ErrorCode))
        string TypeOfFault = ErrorAttributes.GetFaultTypeAttribute(ErrorCode);


        try
        {
            string strFullName = typeof(SystemEx).AssemblyQualifiedName;

            int SystemExLength = "SystemEx".Length;

            strFullName = TypeOfFault + strFullName.Substring(SystemExLength, strFullName.Length - SystemExLength);

            Type type = Type.GetType(strFullName);
            fault = (Fault)Activator.CreateInstance(type);
        }
        catch
        {
            fault = new SystemEx();
        }
        //if (TypeOfFault == typeof(SystemEx).ToString()) fault = new SystemEx();
        ////else if (TypeOfFault == typeof(XMLVendFaultEx).ToString()) fault = new XMLVendFaultEx();
        //else if (TypeOfFault == typeof(ReversalEx).ToString()) fault = new ReversalEx();
        //else if (TypeOfFault == typeof(ConfirmationEx).ToString()) fault = new ConfirmationEx();
        //else if (TypeOfFault == typeof(LastResponseEx).ToString()) fault = new LastResponseEx();
        //else if (TypeOfFault == typeof(XMLVendSchemaEx).ToString()) fault = new XMLVendSchemaEx();
        //else if (TypeOfFault == typeof(ClientIDSSLEx).ToString()) fault = new ClientIDSSLEx();
        //else if (TypeOfFault == typeof(UseCaseSupportEx).ToString()) fault = new UseCaseSupportEx();
        //else if (TypeOfFault == typeof(BusinessRuleEx).ToString()) fault = new BusinessRuleEx();
        
        
        
        //else if (TypeOfFault == typeof(DuplicateMsgIDEx).ToString()) fault = new DuplicateMsgIDEx();
        //else if (TypeOfFault == typeof(IndeterminateLastResponseEx).ToString()) fault = new IndeterminateLastResponseEx();
        //else if (TypeOfFault == typeof(OtherDataEx).ToString()) fault = new OtherDataEx();
        //else if (TypeOfFault == typeof(AccNumMeterLinkEx).ToString()) fault = new AccNumMeterLinkEx();
        //else if (TypeOfFault == typeof(GracePurchaseEx).ToString()) fault = new GracePurchaseEx();
        //else if (TypeOfFault == typeof(MeterDataMisMatchEx).ToString()) fault = new MeterDataMisMatchEx();
        //else if (TypeOfFault == typeof(UnknownMeterFBEEx).ToString()) fault = new UnknownMeterFBEEx();
        //else if (TypeOfFault == typeof(EngParameterEx).ToString()) fault = new EngParameterEx();
        //else if (TypeOfFault == typeof(AccTypeEx).ToString()) fault = new AccTypeEx();
        //else if (TypeOfFault == typeof(AccNumEx).ToString()) fault = new AccNumEx();
        //else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        //    else if (TypeOfFault == typeof().ToString()) fault = new ();
        
        
        
        //else if (TypeOfFault == typeof(VerifyTokenEx).ToString()) fault = new VerifyTokenEx();
        //else if (TypeOfFault == typeof(UpdateMtrKeySameEx).ToString()) fault = new UpdateMtrKeySameEx();
        //else if (TypeOfFault == typeof(UnknownMeterUpdateMtrKeyEx).ToString()) fault = new UnknownMeterUpdateMtrKeyEx();
        //else if (TypeOfFault == typeof(UnknownMeterEx).ToString()) fault = new UnknownMeterEx();
        //else if (TypeOfFault == typeof(ServiceChrgEx).ToString()) fault = new ServiceChrgEx();
        //else if (TypeOfFault == typeof(StartBatchEx).ToString()) fault = new StartBatchEx();
        //else if (TypeOfFault == typeof(RequireMeterCardEx).ToString()) fault = new RequireMeterCardEx();
        //else if (TypeOfFault == typeof(ReprintEx).ToString()) fault = new ReprintEx();
        //else if (TypeOfFault == typeof(ReprintEndBatchEx).ToString()) fault = new ReprintEndBatchEx();
        //else if (TypeOfFault == typeof(ReprintDepositSlipEx).ToString()) fault = new ReprintDepositSlipEx();
        //else if (TypeOfFault == typeof(InsufficientMeterDataEx).ToString()) fault = new InsufficientMeterDataEx();
        //else if (TypeOfFault == typeof(EndBatchEx).ToString()) fault = new EndBatchEx();
        //else if (TypeOfFault == typeof(DebtEx).ToString()) fault = new DebtEx();
        //else if (TypeOfFault == typeof(CheckBatchTotalEx).ToString()) fault = new CheckBatchTotalEx();
        //else if (TypeOfFault == typeof(CancelEx).ToString()) fault = new CancelEx();
        //else if (TypeOfFault == typeof(ConfirmCustomerEx).ToString()) fault = new ConfirmCustomerEx();
        //else if (TypeOfFault == typeof(ClientIDAuthorisationEx).ToString()) fault = new ClientIDAuthorisationEx();
        //else if (TypeOfFault == typeof(VendorCreditEx).ToString()) fault = new VendorCreditEx();
        //else if (TypeOfFault == typeof(STSDataEx).ToString()) fault = new STSDataEx();
        //else if (TypeOfFault == typeof(BlockedMeterEx).ToString()) fault = new BlockedMeterEx();
        //else if (TypeOfFault == typeof(FBEEx).ToString()) fault = new FBEEx();
        //else if (TypeOfFault == typeof(LatestKRNEx).ToString()) fault = new LatestKRNEx();
        //else if (TypeOfFault == typeof(SGCAuthorisationEx).ToString()) fault = new SGCAuthorisationEx();
        //else if (TypeOfFault == typeof(MSNOCheckDigitEx).ToString()) fault = new MSNOCheckDigitEx();
        //else if (TypeOfFault == typeof(RequestAuthorisationEx).ToString()) fault = new RequestAuthorisationEx();

        //else fault = new SystemEx();

        //fault.desc = ErrorAttributes.GetErrorString(ErrorCode);
        return fault;
    }
}
