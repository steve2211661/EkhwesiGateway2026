using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SMSVMA.DLL;
using SMSVMA.BLL;
using SMSVMA.DataUtils.BOFunctions;
using FreshSystems.Utils;
using SMSVend.ErrorCodes;

/// <summary>
/// Summary description for MeterSpecificEngFunctions
/// </summary>
public class MeterSpecificEngFunctions
{
    //private SMSVMA.DataUtils.TokenFunctions.TokenGeneration m_objTokenGeneration;
    //internal SMSVMA.DataUtils.TokenFunctions.TokenGeneration TokenGeneration()
    //{
    //    if (m_objTokenGeneration == null)
    //    {
    //        bool TestMode = System.Environment.MachineName.ToUpper().Substring(0, 3) != "EKH" &&
    //                        System.Environment.MachineName.ToUpper().Substring(0, 3) != "VAL";

    //        string strPrepaidWorldTokenServiceURL = System.Configuration.ConfigurationManager.AppSettings["PrepaidWorldTokenService"];
    //        if (string.IsNullOrEmpty(strPrepaidWorldTokenServiceURL))
    //            throw new Exception("You did not provide the app setting: PrepaidWorldTokenService");

    //        m_objTokenGeneration = new SMSVMA.DataUtils.TokenFunctions.TokenGeneration(strPrepaidWorldTokenServiceURL, "0000000000001", "Daryl", "Password#@01", TestMode);
    //    }

    //    return m_objTokenGeneration;
    //}

    public user currentUser { get; set; }

    public MeterSpecificEngFunctions(user User)
    {
        this.currentUser = User;
    }

    private MeterDetail ExtractMeterDetail(MeterSpecificEngReq meterSpecificEngReq)
    {
        if (meterSpecificEngReq.idMethod.meterIdentifier is MeterDetail)
            return (MeterDetail)meterSpecificEngReq.idMethod.meterIdentifier;
        else
            throw new NotImplementedException("Use meter detail");
    }

    private bool HasSGCPermission(string SGC, string strMeterNumber, BLLManagingAgentPermission.ManagingAgentPermissionOptions permission, BLLRole.UserRole role)
    {
        if (currentUser.CheckUserRole(BLLRole.UserRole.Admin))
            return true;

        MeterResourceTypes eMeterResourceType;
        unit objUnit = new BLLUnit(null).SelectByAnyMeterNumber(strMeterNumber, out eMeterResourceType);



        if (objUnit == null)
        {
            if (!currentUser.CheckUserRole(BLLRole.UserRole.MAAdmin) ||
               ! new BLLManagingAgentPermission().DoesManagingAgentHavePermission(BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngTokensForUnregisteredMeters, currentUser.userMAID))
                throw new GenericException(ERROR.EKH_INCORRECT_PERMISSIONS);
        }

        //int TargetManagingAgentID;
        //if (objUnit == null)
        //{
        //    if (currentUser.CheckUserRole(BLLRole.UserRole.MAAdmin))
        //        TargetManagingAgentID = currentUser.userMAID;
        //    else
        //        throw new GenericException(ERROR.EKH_INCORRECT_PERMISSIONS);
        //}
        //else
        //{
        //    /* If the user is a MA Admin then just carry on
        //     * but if the user is a std user then make sure that they have have to the bc that the meter is in
        //     */

        //    //need more code

        //    TargetManagingAgentID = objUnit.bodycorporateregion.bodycorporate.bcMAID;
        //}


        //if (!currentUser.CheckUserRole(BLLRole.UserRole.Admin))
        //{
            currentUser.CheckUserRoleAndBodyCorpAgainstMeter(objUnit.bodycorporateregion.bodycorporate, role);
            
            
            //currentUser.CheckUserAgainstManagingAgent(TargetManagingAgentID);
            
            
            //if (TargetManagingAgentID != currentUser.userMAID)
            //    throw new GenericException(ERROR.EKH_INCORRECT_PERMISSIONS);
        //}

        return new BLLManagingAgentPermission().DoesManagingAgentHavePermissionToSGC(currentUser.userMAID, SGC, permission);
    }

    internal EngKCTResp ProcessMeterSpecificEngReq(EngKCTReq engKCTReq)
    {
        string strMeterNumber;
        if (engKCTReq.idMethod.meterIdentifier is MeterDetail)
            strMeterNumber = ((MeterDetail)engKCTReq.idMethod.meterIdentifier).msno;
        else if (engKCTReq.idMethod.meterIdentifier is MeterNumber)
            strMeterNumber = ((MeterNumber)engKCTReq.idMethod.meterIdentifier).msno;
        else
            throw new NotImplementedException();

        if (engKCTReq.kctData == null)
            throw new Exception("Key Change Data is not populated");

        //if (!currentUser.CheckUserRole(BLLRole.UserRole.Admin))
        //{
            /* Global Administrator can make key changes for any meter.
             * MA & PA Administrator can make keychanges for 'not registered' meters and meters in their own MA and all of their sub MA's 
             * Other users can only make meters in their assigned body corporates
             */ 

            //int ManagingAgentID = objUnit.bodycorporateregion.bodycorporate.bcMAID;

            if (!HasSGCPermission(engKCTReq.kctData.fromSGC, strMeterNumber, BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngToken, BLLRole.UserRole.KeyChange))
                throw new CustomerSpecificException(ERROR.NO_PERMISSION_OR_INVALID_SGC, "You do not have permission or invalid 'From SGC'");
            else if (!HasSGCPermission(engKCTReq.kctData.toSGC, strMeterNumber, BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngToken, BLLRole.UserRole.KeyChange))
                throw new CustomerSpecificException(ERROR.NO_PERMISSION_OR_INVALID_SGC, "You do not have permission or invalid 'To SGC'");
        //}

        if (!(engKCTReq.kctData.fromKRN.IsInteger() &&
            engKCTReq.kctData.fromTI.IsInteger() &&
            engKCTReq.kctData.toKRN.IsInteger() &&
            engKCTReq.kctData.toTI.IsInteger()))
            throw new Exception("Check the KRN and TI values. They must be whole numbers.");

        int nID;
        SMSVMA.PrepaidWorldTokenQueue.SendRequests.KeyChangeTokens objKeyChangeTokens = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().MakeKeyChangeToken(strMeterNumber,
            engKCTReq.kctData.fromSGC, GetKRN(engKCTReq.kctData.fromKRN), GetTI(engKCTReq.kctData.fromTI),
            engKCTReq.kctData.toSGC, GetKRN(engKCTReq.kctData.toKRN), GetTI(engKCTReq.kctData.toTI), out nID);

        if (new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().TestMode)
            engKCTReq.kctData.fromTI = engKCTReq.kctData.toTI = "0";

        EngKCTResp engKCTResp = new EngKCTResp()
        {
            engKCToken = new KCTokenIssue()
            {
                token = new KCToken()
                {
                    set1stMeterKey = new STS1Token() { stsCipher = objKeyChangeTokens.CodingToken1 },
                    set2ndMeterKey = new STS1Token() { stsCipher = objKeyChangeTokens.CodingToken2 }
                },
                kctData = engKCTReq.kctData,
                desc = "Key Change Token",
                meterDetail = new MeterDetail()
                {
                    krn = engKCTReq.kctData.fromKRN,
                    msno = strMeterNumber,
                    sgc = engKCTReq.kctData.fromSGC,
                    ti = engKCTReq.kctData.fromTI
                }
            },
            custMsg = "Please enter both of these tokens into your meter",
            operatorMsg = "Key Change Token successful",
            dispHeader = "Key Change Token",
            respDateTime = DateTime.Now
        };

        return engKCTResp;
    }

    internal PwrLmtResp ProcessMeterSpecificEngReq(PwrLmtReq pwrLmtReq)
    {
        MeterDetail meterDetail = ExtractMeterDetail(pwrLmtReq);

        //unit objUnit = new BLLUnit(null).SelectByMeterNumber(meterDetail.msno);
        //int ManagingAgentID = objUnit.bodycorporateregion.bodycorporate.bcMAID;

        if (!HasSGCPermission(meterDetail.sgc, meterDetail.msno, BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngToken, BLLRole.UserRole.PowerLimit))
            throw new Exception("You do not have permission or invalid SGC");

        decimal nPowerLimit;
        if (!decimal.TryParse(pwrLmtReq.pwrLmt.value, out nPowerLimit))
            throw new Exception("Power Limit is not a valid decimal");
        int ID;
        string Token = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().MakePowerLimitToken(meterDetail.msno, nPowerLimit, meterDetail.sgc, GetKRN(meterDetail.krn), GetTI(meterDetail.ti), out ID);
        if (new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().TestMode)
            meterDetail.ti = "0";

        PwrLmtResp pwrLmtResp = new PwrLmtResp()
        {
            pwrLmtToken = new PwrLmtTokenIssue()
            {
                desc = "Power Limit Token",
                meterDetail = meterDetail,
                pwrLmt = pwrLmtReq.pwrLmt,
                token = new STS1Token()
                {
                    stsCipher = Token
                }
            },
            dispHeader = "Power Limit Token",
            custMsg = "Please enter this token into your meter",
            operatorMsg = "Power Limit Token successful",
            respDateTime = DateTime.Now
        };

        return pwrLmtResp;

  
    }

    internal ClearCreditResp ProcessMeterSpecificEngReq(ClearCreditReq clearCreditReq)
    {
        MeterDetail meterDetail = ExtractMeterDetail(clearCreditReq);

        //unit objUnit = new BLLUnit(null).SelectByMeterNumber(meterDetail.msno);
        //int ManagingAgentID = objUnit.bodycorporateregion.bodycorporate.bcMAID;

        if (!HasSGCPermission(meterDetail.sgc, meterDetail.msno, BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngToken, BLLRole.UserRole.ClearCredit))
            throw new GenericException(ERROR.NO_PERMISSION_OR_INVALID_SGC);

        int nID;
        string Token;
        if (meterDetail.msno.ToString().Substring(0, 2) == "41" || meterDetail.msno.ToString().Substring(0, 2) == "19" || meterDetail.msno.ToString().Substring(0, 2) == "01")
        {
            Token = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().MakeClearWaterCreditToken(meterDetail.msno, meterDetail.sgc, GetKRN(meterDetail.krn), GetTI(meterDetail.ti), out nID);
        }
        else
        {
            Token = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().MakeClearCreditToken(meterDetail.msno, meterDetail.sgc, GetKRN(meterDetail.krn), GetTI(meterDetail.ti), out nID);
        }
            
        if (new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().TestMode)
            meterDetail.ti = "0";

        ClearCreditResp clearCreditResp = new ClearCreditResp()
        {
            clearCredit = new ClearCreditTokenIssue()
            {
                desc = "Clear Credit Token",
                meterDetail = meterDetail,
                token = new STS1Token()
                {
                    stsCipher = Token
                }
            },
            dispHeader = "Clear Credit Token",
            custMsg = "Please enter this token into your meter",
            operatorMsg = "Clear Credit Token successful",
            respDateTime = DateTime.Now
        };

        return clearCreditResp;
    }

    internal ClearTamperResp ProcessMeterSpecificEngReq(ClearTamperReq clearTamperReq)
    {
        MeterDetail meterDetail = ExtractMeterDetail(clearTamperReq);

        //unit objUnit = new BLLUnit(null).SelectByMeterNumber(meterDetail.msno);
        //int ManagingAgentID = objUnit.bodycorporateregion.bodycorporate.bcMAID;

        if (!HasSGCPermission(meterDetail.sgc, meterDetail.msno, BLLManagingAgentPermission.ManagingAgentPermissionOptions.EngToken, BLLRole.UserRole.ClearTamper))
            throw new Exception("You do not have permission or invalid SGC");

        int nID;
        string Token = new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().MakeClearTamperToken(meterDetail.msno, meterDetail.sgc, GetKRN(meterDetail.krn), GetTI(meterDetail.ti), out nID);
        if (new SMSVMA.DataUtils.Transactions.PerformCreditVendRequest().TokenGeneration().TestMode)
            meterDetail.ti = "0";

        ClearTamperResp clearTamperResp = new ClearTamperResp()
        {
            clearTamper = new ClearTamperTokenIssue()
            {
                desc = "Clear Tamper Token",
                meterDetail = meterDetail,
                token = new STS1Token()
                {
                    stsCipher = Token
                }
            },
            dispHeader = "Clear Tamper Token",
            custMsg = "Please enter this token into your meter",
            operatorMsg = "Clear Tamper Token successful",
            respDateTime = DateTime.Now
        };

        return clearTamperResp;
    }

    private int GetKRN(string krn)
    {
        int nKRN;
        if (!int.TryParse(krn, out nKRN))
            throw new Exception("KRN is not a valid number");
        return nKRN;
    }

    private int GetTI(string ti)
    {
        int nTI;
        if (!int.TryParse(ti, out nTI))
            throw new Exception("TI is not a valid number");
        return nTI;
    }
}
