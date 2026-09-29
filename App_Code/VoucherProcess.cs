using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Text;
using System.Security.Cryptography;
using System.IO;

/// <summary>
/// Summary description for VoucherProcessFunctions
/// </summary>
public class VoucherProcess
{
    public static byte[] CreateSecureVoucher(string VoucherNumber)
    {               
        string keystring =  "F7CFCCA622F8116E9CB11F92ECF68D6FEDA2DE9645C4C71D496E09F968BDC54D";
        string ivstring =   "6629016938E1FDDA9649F1933919AF4E";

        byte[] Key = new byte[32];
        byte[] IV = new byte[16];

        for (int i=0;i<Key.Length;i++)
        {
            Key[i] = HexToByte(keystring.Substring(i * 2, 2));
        
        }
        for (int i = 0; i < IV.Length; i++)
        {
            IV[i] = HexToByte(ivstring.Substring(i * 2, 2));
        }

        return CreateSecureVoucher(VoucherNumber, Key, IV);
    }

    private static byte HexToByte(string hex)
    {
        if (hex.Length > 2 || hex.Length <= 0)
            throw new ArgumentException("hex must be 1 or 2 characters in length");
        byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        return newByte;
    }

    private static byte[] CreateSecureVoucher(string VoucherNumber, byte[] key, byte[] IV)
    {
        ASCIIEncoding textConverter = new ASCIIEncoding();
        RijndaelManaged myRijndael = new RijndaelManaged();
        byte[] encrypted;
        byte[] toEncrypt;
        //Get an encryptor.
        ICryptoTransform encryptor = myRijndael.CreateEncryptor(key, IV);

        //Encrypt the data.
        MemoryStream msEncrypt = new MemoryStream();
        CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

        //Convert the data to a byte array.
        toEncrypt = textConverter.GetBytes(VoucherNumber);

        //Write all data to the crypto stream and flush it.
        csEncrypt.Write(toEncrypt, 0, toEncrypt.Length);
        csEncrypt.FlushFinalBlock();

        //Get encrypted array of bytes.
        encrypted = msEncrypt.ToArray();
        return encrypted;
    }
}
