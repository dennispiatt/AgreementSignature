using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuesPechkin;

namespace AgreementSignature
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var db = new OperationsCockpitContext())
            {
                Console.Clear();
                int _aSigId;
                if (args.Count() == 1)
                {
                    _aSigId = int.Parse(args[0]);
                }
                else
                {
                    Console.Write("\nEnter AgreementSignatureId ");
                    _aSigId = int.Parse(Console.ReadLine());

                }
                var _agreementFormatter = new AgreementFormatter();
                var _agreementSignatures = db.AgreementSignatures.Where(s => s.Id == _aSigId).ToList();
                foreach (var _aSig in _agreementSignatures)
                {

                    Console.WriteLine(_aSig.District.ISD.Region.Name);
                    Console.WriteLine(_aSig.District.Name);
                    byte[] body;
                    string mimeType;
                    _agreementFormatter.GetFormattedAgreement(_aSig, out body, out mimeType);

                    _aSig.SignedBody = body;
                    _aSig.SignedMimeType = mimeType;
                    //string fileName = "d:\\Temp\\test.pdf";
                    //System.IO.File.WriteAllBytes(fileName, body);
                    //Console.WriteLine(fileName);
                    db.SaveChanges();
                }
            }
        }
    }

    public class AgreementFormatter
    {
        internal void GetFormattedAgreement(AgreementSignature signature, out byte[] body, out string mimeType)
        {
            var _htmlToPdfConverter = new HtmlToPdfConverterProvider();
            body = signature.Agreement.Body;
            mimeType = signature.Agreement.MimeType;

            if (mimeType == "text/html")
            {
                body = FormatAgreement(signature);
                body = _htmlToPdfConverter.Convert(body, signature.Agreement.Title);
                mimeType = "application/pdf";
            }
        }
        private byte[] FormatAgreement(AgreementSignature signature, string placeholderUserName = null)
        {
            //var user = signature.AgreementStatus == AgreementStatus.Pending
            //    ? _userRepository.Get(u => u.UserName == placeholderUserName).Single()
            //    : signature.SigningUser;
            var user = signature.SigningUser;
            var claimSets = user.ClaimSets.Where(dcs => dcs.Discriminator == "DistrictClaimSet" && dcs.District_Id == signature.District.Id).ToList();

            var roleName = claimSets.Any(role => role.Role == 3)
                ? "District Superintendent / Lead Administrator"
                : user.Title;

            if (signature.Agreement.MimeType != "text/html")
                return signature.Agreement.Body;

            var unformattedBody = Encoding.UTF8.GetString(signature.Agreement.Body);

            string phoneNumber = user.PhoneNumber;
            if (string.IsNullOrWhiteSpace(phoneNumber))
                phoneNumber = signature.District.Phone;

            string statusString;
            switch (signature.AgreementStatus)
            {
                case 1:
                    statusString = "Electronically accepted";
                    if (signature.SigningUser != null)
                        statusString += " by " + signature.SigningUser.Name;
                    if (signature.DateSigned != null)
                        statusString += " on " + signature.DateSigned.Value.ToShortDateString() + " at " + signature.DateSigned.Value.ToLongTimeString();
                    statusString += ".";
                    break;
                case 2:
                    statusString = "Electronically rejected";
                    if (signature.SigningUser != null)
                        statusString += " by " + signature.SigningUser.Name;
                    if (signature.DateSigned != null)
                        statusString += " on " + signature.DateSigned.Value.ToShortDateString() + " at " + signature.DateSigned.Value.ToLongTimeString();
                    statusString += ".";
                    break;
                default:
                    statusString = string.Empty;
                    break;
            }

            var now = signature.DateSigned.GetValueOrDefault(DateTime.Now);
            var formattedBody = string.Format(unformattedBody,
                signature.DateSigned.GetValueOrDefault(now), // 0
                signature.District.ISD.Region.Name, // 1
                signature.District.ISD.Region.Id, // 2
                signature.District.Name, // 3
                string.Format("{0}, {1}, {2} {3}", signature.District.Address, signature.District.City, signature.District.State, signature.District.Zip == null ? string.Empty : signature.District.Zip.Length <= 5 ? signature.District.Zip : signature.District.Zip.Substring(0, 5) + "-" + signature.District.Zip.Substring(5)), // 4
                signature.District.Address, //5
                signature.District.City, // 6
                signature.District.State, // 7
                signature.District.Zip == null ? String.Empty : signature.District.Zip.Length <= 5 ? signature.District.Zip : signature.District.Zip.Substring(0, 5) + "-" + signature.District.Zip.Substring(5), // 8
                phoneNumber, // 9
                OrdinalHelper.GetOrdinalForNumber(now.Day), // 10
                user.Name, // 11
                user.UserName, // Email Address - 12
                roleName, // 13
                statusString, //"Electronically accepted by Foo on 1/2/3" //Or string.Empty or "Electronically declined by Foo on 1/2/3" 14
                now.Month, // 15
                now.Year // 16
                );

            return Encoding.UTF8.GetBytes(formattedBody);
        }
    }

    public static class OrdinalHelper
    {
        public static string GetOrdinalForNumber(int day)
        {
            if (day > 0)
            {
                if (day % 10 == 1 && day % 100 != 11)
                    return "st";
                else if (day % 10 == 2 && day % 100 != 12)
                    return "nd";
                else if (day % 10 == 3 && day % 100 != 13)
                    return "rd";
                else
                    return "th";
            }
            else
                return string.Empty;
        }
    }
    public class HtmlToPdfConverterProvider
    {
        private static readonly IConverter Converter;

        static HtmlToPdfConverterProvider()
        {
            //The docs say this should be created once, and only once.  And they are right.  If you try calling
            // it twice, the app pool hangs.
            if (Converter == null)
            {
                Converter = new ThreadSafeConverter(
                    new RemotingToolset<PdfToolset>(
                        new Win64EmbeddedDeployment(
                            new TempFolderDeployment())));
            }
        }



        internal byte[] Convert(byte[] input, string title)
        {
            //If this is HTML, they want to download in PDF format.  So covert it to PDF.  I tested out a couple libraries,
            //  and I found this one to be the most reliable, and quickest to get up and running.  There may be a better choice,
            //  but this was the choice I made with the time constraints.  The biggest problem I had with other libraries is
            //  that it wouldn't be able to convert the document, it didn't like the data uris.  This ends up calling a native
            //  library and I set the web project to run in x64 mode because of it.

            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                    {
                        DocumentTitle = title,
                    },
                Objects = {
                            new ObjectSettings { RawData = input },
                        }
            };

            string errors = string.Empty;
            Converter.Error += (pechkin, text) => { errors += text + Environment.NewLine; };

            byte[] pdfBuf = Converter.Convert(document);

            if (!string.IsNullOrWhiteSpace(errors))
                throw new ApplicationException("Error converting to PDF: " + errors);

            return pdfBuf;
        }
    }
}