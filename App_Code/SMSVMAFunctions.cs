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
using System.Collections.Generic;
using SMSVMA.BLL;
using SMSVMA.DLL;

/// <summary>
/// Summary description for SMSVMAFunctions
/// </summary>
public static class SMSVMAFunctions
{
    public static BaseVendResp[] GetTransactions(int UnitID, DateTime? FromDate, DateTime? ToDate, int PageNumber, int PageSize, out int NumPages)//string msno, 
    {
        BLLUnit bllUnit = new BLLUnit(null);

        //MeterResourceTypes eMeterResourceType;
        //unit currentUnit = bllUnit.SelectByAnyMeterNumber(msno, out eMeterResourceType);
        unit currentUnit = bllUnit.SelectByID(UnitID);
        if (currentUnit == null)
            throw new Exception("Meter number not found");

        BLLTransaction bllTransaction = new BLLTransaction();
        IQueryable<SMSVMA.DLL.Transaction> lstTransactions = bllTransaction.SelectByUnitIDAndTransactionTypeID(currentUnit.unitID, BLLTransaction.SelectVendTransactionTypes());
        if (FromDate.HasValue)
            lstTransactions = lstTransactions.Where(c => c.TransactionDate > FromDate);
        if (ToDate.HasValue)
            lstTransactions = lstTransactions.Where(c => c.TransactionDate < ToDate);

        NumPages = (int)Math.Ceiling((decimal)lstTransactions.Count() / (decimal)PageSize);

        lstTransactions = lstTransactions.OrderByDescending(c => c.TransactionDate).Skip(PageSize * PageNumber).Take(PageSize);

        List<BaseVendResp> lstBaseVendResp = new List<BaseVendResp>();
        foreach (SMSVMA.DLL.Transaction transaction in lstTransactions)
        {
            BaseVendResp objBaseVendResp = GetBaseVendResp(transaction);
            //if (objBaseVendResp == null)
            //    objBaseVendResp = GetBaseVendRespViaQueue(transaction, msno);

            if (objBaseVendResp != null)
                lstBaseVendResp.Add(objBaseVendResp);
        }

        return lstBaseVendResp.ToArray();
    }

    private static BaseVendResp GetBaseVendResp(Transaction transaction)
    {
        if (transaction.ResponseData == null)
        {
            return null;
        }
        try
        {
            SMSVMA.DataUtils.Transactions.BaseResp objResp;
            if (transaction.ResponseData.Name == typeof(SMSVMA.DataUtils.Transactions.CreditVendResp).Name)
                objResp = SMSVMA.VMACommon.CommonFunctions.DeserializeObject(transaction.ResponseData.ToString(), typeof(SMSVMA.DataUtils.Transactions.CreditVendResp)) as SMSVMA.DataUtils.Transactions.BaseResp;
            else if (transaction.ResponseData.Name == typeof(SMSVMA.DataUtils.Transactions.PayAccResp).Name)
                objResp = SMSVMA.VMACommon.CommonFunctions.DeserializeObject(transaction.ResponseData.ToString(), typeof(SMSVMA.DataUtils.Transactions.PayAccResp)) as SMSVMA.DataUtils.Transactions.BaseResp;
            else
                throw new NotImplementedException();


            return objResp.ConvertToGatewayCreditVendResp(false);
        }
        catch
        {
            return null;
        }
    }

    //private static BaseVendResp GetBaseVendRespViaQueue(Transaction transaction, string msno)
    //{
    //    if (transaction.SMSVMAqueue != null)
    //    {
    //        SMSVMA.BLL.BLLTransaction.TransactionTypeOptions enTransactionType = (BLLTransaction.TransactionTypeOptions)transaction.TransactionTypeID;

    //        List<Tx> lstTx = new List<Tx>();

    //        lstTx.Add(new CreditVendTx()
    //        {
    //            receiptNo = transaction.TransactionID.ToString(),
    //            amt = new Currency()
    //            {
    //                symbol = "R",
    //                value = transaction.TransactionAmount
    //            },
    //            creditTokenIssue = new CreditTokenIssue()
    //            {

    //                meterDetail = new MeterDetail() { msno = msno },
    //                resource = new Electricity(),
    //                token = new STS1Token() { stsCipher = transaction.SMSVMAqueue.queueActivationCode.Replace(" ", "") },
    //                units = new Units() { siUnit = "kWh", value = transaction.SMSVMAqueue.queueRandValue },
    //            }
    //        });

    //        return new CreditVendResp()
    //        {
    //            respDateTime = transaction.TransactionDate,
    //            creditVendReceipt = new CreditVendReceipt()
    //            {
    //                receiptNo = transaction.TransactionID.ToString(),
    //                transactions = new Transactions()
    //                {
    //                    tenderAmt = new Currency()
    //                    {
    //                        symbol = "R",
    //                        value = transaction.TransactionAmount
    //                    },
    //                    tx = lstTx.ToArray()
    //                }
    //            },
    //            dispHeader = enTransactionType.Description()
    //        };
    //    }
    //    else
    //        return null;
    //}

    public static BaseVendResp ConvertToGatewayCreditVendResp(this SMSVMA.DataUtils.Transactions.BaseResp objResp, bool ConsolidateSubAccounts)
    {
        if (objResp is SMSVMA.DataUtils.Transactions.CreditVendResp)
            return (objResp as SMSVMA.DataUtils.Transactions.CreditVendResp).ConvertToGatewayCreditVendResp(ConsolidateSubAccounts);
        else if (objResp is SMSVMA.DataUtils.Transactions.PayAccResp)
            return (objResp as SMSVMA.DataUtils.Transactions.PayAccResp).ConvertToGatewayCreditVendResp();
        else
            throw new Exception("Invalid Type: " + objResp.GetType());
    }

    public static CreditVendResp ConvertToGatewayCreditVendResp(this SMSVMA.DataUtils.Transactions.CreditVendResp objCreditVendResp, bool ConsolidateSubAccounts)
    {
        CreditVendResp creditVendResp = new CreditVendResp()
        {
            custMsg = objCreditVendResp.custMessage,
            clientStatus = new ClientStatus()
            {
                availCredit = ConvertToGatewayObject(objCreditVendResp.clientStatus.accountBalance)
            },
            dispHeader = "Credit Token",
            respDateTime = objCreditVendResp.respDateTime,
            creditVendReceipt = new CreditVendReceipt()
            {
                receiptNo = objCreditVendResp.creditVendReceipt.receiptNo.ToString(),
                transactions = new Transactions()
                {
                    change = new Currency() { value = 0 },
                    lessRound = ConvertToGatewayObject(objCreditVendResp.creditVendReceipt.transactions.lessRound),
                    tenderAmt = ConvertToGatewayObject(objCreditVendResp.creditVendReceipt.transactions.tenderAmt),
                    tx = ConvertToGatewayTx(objCreditVendResp.creditVendReceipt.transactions.tx, 
                                            objCreditVendResp.creditVendReceipt.transactions.managingAgentFee, 
                                            objCreditVendResp.creditVendReceipt.transactions.thirdPartyVendingFee, 
                                            objCreditVendResp.creditVendReceipt.transactions.perTransactionRecoveryFee,
                                            ConsolidateSubAccounts),
                }
            },
            utility = new UtilityDetail()
            {
                name = "Ekhwesi"
            },
            custVendDetail = ConvertToGatewayObject(objCreditVendResp.customerDetails)
        };

        return creditVendResp;
    }

    public static PayAccResp ConvertToGatewayCreditVendResp(this SMSVMA.DataUtils.Transactions.PayAccResp objResp)
    {
        PayAccResp creditVendResp = new PayAccResp()
        {
            custMsg = objResp.custMessage,
            clientStatus = new ClientStatus()
            {
                availCredit = ConvertToGatewayObject(objResp.clientStatus.accountBalance)
            },
            dispHeader = "Pay Account",
            respDateTime = objResp.respDateTime,
            payAccReceipt = new PayAccReceipt()
            {
                receiptNo = objResp.payAccReceipt.receiptNo.ToString(),
                transactions = new Transactions()
                {
                    change = new Currency() { value = 0 },
                    lessRound = ConvertToGatewayObject(objResp.payAccReceipt.transactions.lessRound),
                    tenderAmt = ConvertToGatewayObject(objResp.payAccReceipt.transactions.tenderAmt),
                    tx = ConvertToGatewayTx(objResp.payAccReceipt.transactions.tx, objResp.payAccReceipt.transactions.managingAgentFee, objResp.payAccReceipt.transactions.thirdPartyVendingFee,null, false),
                }
            },
            utility = new UtilityDetail()
            {
                name = "Ekhwesi"
            },
            custVendDetail = ConvertToGatewayObject(objResp.customerDetails)
        };

        return creditVendResp;
    }

    private static CustVendDetail ConvertToGatewayObject(SMSVMA.DataUtils.Transactions.CustomerDetails customerDetails)
    {
        if (customerDetails != null)
        {
            return new CustVendDetail()
            {
                accNo = customerDetails.ownerID.ToString(),
                name = customerDetails.Name,
                contactNo = customerDetails.ContactNumber,
            };
        }
        else
            return new CustVendDetail();
    }

    private static Tx[] ConvertToGatewayTx(SMSVMA.DataUtils.Transactions.Tx[] colTx, 
                                            SMSVMA.DataUtils.Transactions.ExtraFee managingAgentFee, 
                                            SMSVMA.DataUtils.Transactions.ExtraFee thirdPartyVendingFee, 
                                            SMSVMA.DataUtils.Transactions.ExtraFee perTransactionRecoveryFee,
                                            bool ConsolidateSubAccounts)
    {
        List<Tx> lstTx = new List<Tx>();

        //decimal nTaxAmount = 0;
        //foreach (SMSVMA.DataUtils.Transactions.Tx tx in colTx)
        //{
        //    if (tx.tax != null && tx.tax.amt != null)
        //    {
        //        nTaxAmount += tx.tax.amt.value;
        //        tx.tax.amt.value = 0;
        //    }
        //}

        //if (managingAgentFee != null && managingAgentFee.tax != null && managingAgentFee.tax.amt != null)
        //{
        //    nTaxAmount += managingAgentFee.tax.amt.value;
        //    managingAgentFee.tax.amt.value = 0;
        //}

        //SMSVMA.DataUtils.Transactions.Tx firstTx = colTx.FirstOrDefault(c => c is SMSVMA.DataUtils.Transactions.CreditVendTx && c.tax != null && c.tax.amt != null);
        //if (firstTx == null)
        //    firstTx = colTx.First(c => c.tax != null && c.tax.amt != null);

        //if (firstTx != null)
        //{
        //    firstTx.tax.amt.value = nTaxAmount;
        //}

        if (!ConsolidateSubAccounts)
        {
            foreach (SMSVMA.DataUtils.Transactions.Tx tx in colTx)
            {
                if (tx is SMSVMA.DataUtils.Transactions.CreditVendTx)
                {
                    lstTx.Add(ConvertToGatewayTx(tx as SMSVMA.DataUtils.Transactions.CreditVendTx));
                }
                else if (tx is SMSVMA.DataUtils.Transactions.FixedFeeTx)
                {
                    lstTx.Add(ConvertToGatewayTx(tx as SMSVMA.DataUtils.Transactions.FixedFeeTx));
                }
                else if (tx is SMSVMA.DataUtils.Transactions.SubAccountTx)
                {
                    lstTx.Add(ConvertToGatewayTx(tx as SMSVMA.DataUtils.Transactions.SubAccountTx));
                }
            }

            if (managingAgentFee != null && managingAgentFee.amt != null)
            {
                ServiceChrgTx objServiceChrgTx = new ServiceChrgTx()
                {
                    accDesc = "Managing Agent Fee",
                    amt = ConvertToGatewayObject(managingAgentFee.amt),
                    tax = new Tax[] { ConvertToGatewayObject(managingAgentFee.tax) },
                    receiptNo = "",
                    accNo = "",
                };

                lstTx.Add(objServiceChrgTx);
            }

            if (thirdPartyVendingFee != null && thirdPartyVendingFee.amt != null)
            {
                ServiceChrgTx objServiceChrgTx = new ServiceChrgTx()
                {
                    accDesc = "3rd Party Vending Fee",
                    amt = ConvertToGatewayObject(thirdPartyVendingFee.amt),
                    tax = new Tax[] { ConvertToGatewayObject(thirdPartyVendingFee.tax) },
                    receiptNo = "",
                    accNo = "",
                };

                lstTx.Add(objServiceChrgTx);
            }

            if (perTransactionRecoveryFee != null && perTransactionRecoveryFee.amt != null)
            {
                ServiceChrgTx objServiceChrgTx = new ServiceChrgTx()
                {
                    accDesc = "Transaction Fee",
                    amt = ConvertToGatewayObject(perTransactionRecoveryFee.amt),
                    tax = new Tax[] { ConvertToGatewayObject(perTransactionRecoveryFee.tax) },
                    receiptNo = "",
                    accNo = "",
                };

                lstTx.Add(objServiceChrgTx);
            }
        }
        else
        {
            decimal nAppliedFeesAmount = 0;
            decimal nAppliedFeeTaxAmount = 0;

            decimal nDebtFeesAmount = 0;
            decimal nDebtFeesTaxAmount = 0;

            SMSVMA.DataUtils.Transactions.Currency curExample = null;
            SMSVMA.DataUtils.Transactions.Tax taxExample = null;

            foreach (SMSVMA.DataUtils.Transactions.Tx tx in colTx)
            {
                if (taxExample == null && tx.tax != null && tx.tax.rate > 0)
                    taxExample = tx.tax;

                if (curExample == null)
                    curExample = tx.amt;

                if (tx is SMSVMA.DataUtils.Transactions.CreditVendTx)
                {
                    lstTx.Add(ConvertToGatewayTx(tx as SMSVMA.DataUtils.Transactions.CreditVendTx));
                }
                else if (tx is SMSVMA.DataUtils.Transactions.FixedFeeTx || tx is SMSVMA.DataUtils.Transactions.SubAccountTx)
                {
                    //lstTx.Add(ConvertToGatewayTx(tx as SMSVMA.DataUtils.Transactions.FixedFeeTx));
                    //nAppliedFeesAmount += tx.amt.value;
                    //nAppliedFeeTaxAmount += tx.taxAmount;
                    nDebtFeesAmount += tx.amt.value;
                    nDebtFeesTaxAmount += tx.taxAmount;
                }
                //else if (tx is SMSVMA.DataUtils.Transactions.SubAccountTx)
                //{
                //    nDebtFeesAmount += tx.amt.value;
                //    nDebtFeesTaxAmount += tx.taxAmount;
                //}
                else
                    throw new NotImplementedException("Invalid Type - " + tx.GetType().Name);
            }

            if (managingAgentFee != null && managingAgentFee.amt != null)
            {
                nAppliedFeesAmount += managingAgentFee.amt.value;
                nAppliedFeeTaxAmount += managingAgentFee.tax != null && managingAgentFee.tax.amt != null ? managingAgentFee.tax.amt.value : 0;
            }

            if (thirdPartyVendingFee != null && thirdPartyVendingFee.amt != null)
            {
                nAppliedFeesAmount += thirdPartyVendingFee.amt.value;
                nAppliedFeeTaxAmount += thirdPartyVendingFee.tax != null && thirdPartyVendingFee.tax.amt != null ? thirdPartyVendingFee.tax.amt.value : 0;
            }

            if (perTransactionRecoveryFee != null && perTransactionRecoveryFee.amt != null)
            {
                nAppliedFeesAmount += perTransactionRecoveryFee.amt.value;
                nAppliedFeeTaxAmount += perTransactionRecoveryFee.tax != null && perTransactionRecoveryFee.tax.amt != null ? perTransactionRecoveryFee.tax.amt.value : 0;
            }
            

            if (nAppliedFeesAmount > 0)
            {
                if (curExample == null) curExample = new SMSVMA.DataUtils.Transactions.Currency() { symbol = "R", symbolOnLeft = true, value = 0 };
                if (taxExample == null) taxExample = new SMSVMA.DataUtils.Transactions.VAT() { amt = new SMSVMA.DataUtils.Transactions.Currency() , rate = 0 };

                SMSVMA.DataUtils.Transactions.Tax tax = new SMSVMA.DataUtils.Transactions.VAT()
                {
                    rate = taxExample.rate,
                    amt = new SMSVMA.DataUtils.Transactions.Currency()
                    {
                        value = nAppliedFeeTaxAmount,
                        symbol = curExample.symbol,
                        symbolOnLeft = curExample.symbolOnLeft
                    }
                };

                SMSVMA.DataUtils.Transactions.FixedFeeTx objServiceChrgTx = new SMSVMA.DataUtils.Transactions.FixedFeeTx()
                {
                     
                    name = "Service Charge",
                    amt = new SMSVMA.DataUtils.Transactions.Currency()
                    {
                        symbol = curExample.symbol,
                        symbolOnLeft = curExample.symbolOnLeft,
                         value = nAppliedFeesAmount
                    },
                    tax = tax,
                    receiptNo = 0,
                     unitAppliedFeeID = 0
                };

                lstTx.Add(ConvertToGatewayTx(objServiceChrgTx));
            }

            if (nDebtFeesAmount > 0)
            {
                if (curExample == null) curExample = new SMSVMA.DataUtils.Transactions.Currency() { symbol = "R", symbolOnLeft = true, value = 0 };
                if (taxExample == null) taxExample = new SMSVMA.DataUtils.Transactions.VAT() { amt = new SMSVMA.DataUtils.Transactions.Currency(), rate = 0 };

                SMSVMA.DataUtils.Transactions.Tax tax = new SMSVMA.DataUtils.Transactions.VAT()
                {
                    rate = taxExample.rate,
                    amt = new SMSVMA.DataUtils.Transactions.Currency()
                    {
                        value = nDebtFeesTaxAmount,
                        symbol = curExample.symbol,
                        symbolOnLeft = curExample.symbolOnLeft
                    }
                };

                SMSVMA.DataUtils.Transactions.SubAccountTx objServiceChrgTx = new SMSVMA.DataUtils.Transactions.SubAccountTx()
                {

                    name = "Other Charges",
                    amt = new SMSVMA.DataUtils.Transactions.Currency()
                    {
                        symbol = curExample.symbol,
                        symbolOnLeft = curExample.symbolOnLeft,
                        value = nDebtFeesAmount
                    },
                    tax = tax,
                    receiptNo = 0,
                    unitAppliedFeeID = 0
                };

                lstTx.Add(ConvertToGatewayTx(objServiceChrgTx));
            }
        }

        //if (AddDummyCreditVendTx && !lstTx.Any(c => c is CreditVendTx))
        //{
        //    CreditVendTx fakeCreditVendTx = new CreditVendTx()
        //    {
        //        amt = new Currency() { symbol = "R", symbolOnLeft = true, value = 0 },
        //        receiptNo = "0",
        //        tax = new Tax[] { new VAT() { amt = new Currency() { symbol = "R", symbolOnLeft = true, value = 0 } } },
        //        creditTokenIssue = new SaleCredTokenIssue()
        //        {
        //            desc = "Zero value token",
        //            meterDetail = new MeterDetail()
        //            {
        //                msno = "00000000000",
        //                krn = "0",
        //                ti = "0",
        //                sgc = "000000",
        //                meterType = new MeterType()
        //                {
        //                    at = "00",
        //                    tt = "00"
        //                }
        //            },
        //            resource = new Electricity(),

        //            units = new Units() { value = 0, siUnit = "kWh" },
        //            token = new STS1Token() { stsCipher = "00000000000000000000" }
        //        },
        //        tariff = new Tariff()
        //        {
        //            desc = string.Empty
        //        }
        //    };

        //    lstTx.Add(fakeCreditVendTx);
        //}

        return lstTx.ToArray();
    }

    private static CreditVendTx ConvertToGatewayTx(this SMSVMA.DataUtils.Transactions.CreditVendTx objCreditVendTx)
    {
        decimal nUnits = 0;
        string strTariffDesc = string.Empty;
        if (objCreditVendTx.creditTokenIssue.tariffInfo != null)
        {
            nUnits = objCreditVendTx.creditTokenIssue.tariffInfo.TotalUnits;
            strTariffDesc = objCreditVendTx.creditTokenIssue.tariffInfo.ToString();
        }
        else if (objCreditVendTx.creditTokenIssue.units != 0)
        {
            
            //THIS IS PURELY FOR THE OLD FORMAT OF THE RESPONSE. The new format kicked in on 1 september 2010.
            nUnits = objCreditVendTx.creditTokenIssue.units;
            if (objCreditVendTx.creditTokenIssue.kwhRateUsed != 0)
                strTariffDesc = Math.Round(objCreditVendTx.creditTokenIssue.units, 2) + " " + objCreditVendTx.creditTokenIssue.tariffInfo.unitsSymbol + " @ " + Math.Round(objCreditVendTx.creditTokenIssue.kwhRateUsed, 4) + " per " + objCreditVendTx.creditTokenIssue.tariffInfo.unitsSymbol;
        }

        CreditVendTx gatewayCreditVendTx = new CreditVendTx()
        {
            amt = ConvertToGatewayObject(objCreditVendTx.amt),
            receiptNo = objCreditVendTx.receiptNo.ToString(),
            tax = new Tax[] { ConvertToGatewayObject(objCreditVendTx.tax) },
            creditTokenIssue = new SaleCredTokenIssue()
            {
                desc = objCreditVendTx.creditTokenIssue.desc,
                meterDetail = new MeterDetail()
                {
                    msno = objCreditVendTx.creditTokenIssue.meterDetail.meterNumber,
                    krn = objCreditVendTx.creditTokenIssue.meterDetail.krn.ToString(),
                    ti = objCreditVendTx.creditTokenIssue.meterDetail.ti.ToString(),
                    sgc = objCreditVendTx.creditTokenIssue.meterDetail.sgc,
                    meterType = new MeterType()
                    {
                        at = "07",
                        tt = "02"
                    }
                },
                resource = new Electricity(),

                units = new Units() { value = Math.Round(nUnits, 1), siUnit = objCreditVendTx.creditTokenIssue.tariffInfo.unitsSymbol },
                token = new STS1Token() { stsCipher = objCreditVendTx.creditTokenIssue.token.stsCipher }
            },
            tariff = new Tariff()
            {
                name = objCreditVendTx.creditTokenIssue.tariffInfo.tariffName,
                desc = strTariffDesc
            }
            //managingAgentFee = ConvertToGatewayObject(creditVendTx.managingAgentFee)
        };
        return gatewayCreditVendTx;
    }

    private static ServiceChrgTx ConvertToGatewayTx(this SMSVMA.DataUtils.Transactions.FixedFeeTx objFixedFeeTx)
    {
        ServiceChrgTx objServiceChrgTx = new ServiceChrgTx()
        {
            accDesc = objFixedFeeTx.name,
            amt = ConvertToGatewayObject(objFixedFeeTx.amt),
            tax = new Tax[] { ConvertToGatewayObject(objFixedFeeTx.tax) },
            receiptNo = objFixedFeeTx.receiptNo.ToString(),
            accNo = objFixedFeeTx.unitAppliedFeeID.ToString(),
        };

        return objServiceChrgTx;
    }

    private static DebtRecoveryTx ConvertToGatewayTx(this SMSVMA.DataUtils.Transactions.SubAccountTx objAppliedFeeTx)
    {
        DebtRecoveryTx objDebtRecoveryTx = new DebtRecoveryTx()
        {
            accDesc = objAppliedFeeTx.name,
            amt = ConvertToGatewayObject(objAppliedFeeTx.amt),
            tax = new Tax[] { ConvertToGatewayObject(objAppliedFeeTx.tax) },
            receiptNo = objAppliedFeeTx.receiptNo.ToString(),
            accNo = objAppliedFeeTx.unitAppliedFeeID.ToString(),
            balance = ConvertToGatewayObject(objAppliedFeeTx.balance)
        };

        return objDebtRecoveryTx;
    }

    //private static ManagingAgentFee ConvertToGatewayObject(SMSVMA.DataUtils.Transactions.ManagingAgentFee managingAgentFee)
    //{
    //    if (managingAgentFee == null)
    //        return null;
    //    else
    //    {
    //        return new ManagingAgentFee()
    //        {
    //            amt = ConvertToGatewayObject(managingAgentFee.amt),
    //            rate = managingAgentFee.rate,
    //            tax = ConvertToGatewayObject(managingAgentFee.tax)
    //        };
    //    }
    //}

    private static Tax ConvertToGatewayObject(SMSVMA.DataUtils.Transactions.Tax tax)
    {
        if (tax == null)
            return null;
        else
        {
            return new VAT()
            {
                amt = ConvertToGatewayObject(tax.amt,4),
                rate = tax.rate
            };
        }
    }

    private static Currency ConvertToGatewayObject(SMSVMA.DataUtils.Transactions.Currency currency)
    {
        return ConvertToGatewayObject(currency, 2);
    }

    private static Currency ConvertToGatewayObject(SMSVMA.DataUtils.Transactions.Currency currency, int DecimalPlaces)
    {
        if (currency == null)
            return null;
        else
        {
            return new Currency()
            {
                value = Math.Round(currency.value,DecimalPlaces),
                symbol = currency.symbol,
                symbolOnLeft = currency.symbolOnLeft
            };
        }
    }

}


