using eCheque.MICO360.Models;
using Xunit;

namespace eCheque.MICO360.Tests
{
    public class ChequeLayoutTests
    {
        static ChequeProfile P() => new() { Name = "Test", BankName = "Bank", ChequeWidth = 190, ChequeHeight = 85 };

        [Fact]
        public void Default_builds_the_standard_field_set_without_throwing()
        {
            var fields = ChequeLayout.Default(P());
            Assert.Contains(fields, f => f.Key == "Date");
            Assert.Contains(fields, f => f.Key == "Payee");
            Assert.Contains(fields, f => f.Key == "AmountNum");
            Assert.Contains(fields, f => f.Key == "AmountWords");
            Assert.Contains(fields, f => f.Key == "Signature");
        }

        [Fact]
        public void Parse_falls_back_to_default_for_empty_or_bad_json()
        {
            Assert.NotEmpty(ChequeLayout.Parse(P()));                              // empty FieldsJson
            Assert.NotEmpty(ChequeLayout.Parse(new ChequeProfile { FieldsJson = "not json" }));
        }

        [Fact]
        public void Roundtrip_serialize_parse_preserves_fields()
        {
            var p = P();
            var fields = ChequeLayout.Default(p);
            p.FieldsJson = ChequeLayout.Serialize(fields);
            var back = ChequeLayout.Parse(p);
            Assert.Equal(fields.Count, back.Count);
            Assert.Equal(fields[0].Key, back[0].Key);
        }

        [Fact]
        public void ValueFor_maps_each_field_to_the_right_cheque_data()
        {
            var c = new ChequeRecord { ChequeNumber = "000123", PayeeName = "John Smith", Amount = 4956.250m, AmountInWords = "WORDS", AccountNumber = "111222", ChequeDate = new DateTime(2026, 7, 2) };
            Assert.Equal("John Smith", ChequeLayout.ValueFor(new ChequeField { Key = "Payee" }, c));
            Assert.Equal("4,956.250", ChequeLayout.ValueFor(new ChequeField { Key = "AmountNum" }, c));
            Assert.Equal("WORDS", ChequeLayout.ValueFor(new ChequeField { Key = "AmountWords" }, c));
            Assert.Equal("111222", ChequeLayout.ValueFor(new ChequeField { Key = "AccountNumber" }, c));
            Assert.Equal("000123", ChequeLayout.ValueFor(new ChequeField { Key = "ChequeNumber" }, c));
            Assert.Equal("", ChequeLayout.ValueFor(new ChequeField { Key = "Signature" }, c));
            Assert.Equal("02/07/2026", ChequeLayout.ValueFor(new ChequeField { Key = "Date" }, c));
            Assert.Equal("Static", ChequeLayout.ValueFor(new ChequeField { Key = "Custom", CustomText = "Static" }, c));
        }
    }
}
