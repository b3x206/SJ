namespace SJ.Tests;

/// <summary>
/// Contains JSON data to test against.
/// </summary>
public static class TestData
{
    // JSON Data
    // - Root Data
    public const string JsonDataEmptyObject = @"{}", JsonDataEmptyArray = @"[]", JsonDataRootString = @"""Hello world!""",
        JsonDataRootNumber = @"42.42", JsonDataRootBool = @"false", JsonDataRootNull = @"null", DataEmpty = "";

    public const string JsonData1 = @"{
        ""foo"": [""bar"", ""baz""],
        ""idk"": 42,
        ""mango"": { ""6"": 7 }
}";
    public const string JsonDataInvalid = @"{
        ""foo"": ""bar"", ""baz""],
        ""id],123
// Are comments valid? No
";
    public const string JsonData2 = @"{
    ""numbers"": [0, -1, 1.23, 1.0e-5, 1000000],
    ""strings"": {
        ""basic"": ""Hello World"",
        ""escaped"": ""Quote: \"", Backslash: \\, Tab: \t, Newline: \n"",
        ""unicode_BMP"": ""Euro: \u20AC"",
        ""emoji_surrogate"": ""Pizza: \uD83C\uDF55"",
        ""emoji_with_variant"": ""The 🅱 variant: \uD83C\uDD71\uFE0F"",
        ""raw_emoji"": ""🍕"",
        ""non_ascii_literal"": ""你好, ¡Hola!, Grüße""
    },
    ""nesting"": [{
        ""depth_1"": [
            { ""depth_2"": ""We're deep now"" }
        ]
    }],
    ""logic"": [true, false, null],
    ""empty"": { ""obj"": {}, ""arr"": [] }
}";
    // Fire data
    // Creates 1MB of UTF-8 string. Quite wholesome.
    public static readonly string DataEmojiSpam = string.Concat(Enumerable.Repeat("🅱️🅱️🅱️🍄‍🍕🍕🍕🍄‍🍄‍🍕🍄‍🍕🍕🍕🍕🍄‍🍕🍄‍", 10240));
    public const string JsonDataSpamKey = "YoThisDataIsFire";
    public static readonly string JsonDataSpamValue = string.Concat(Enumerable.Repeat("🔥", 32768));
    public static readonly string JsonData3 = $@"{{
    ""{JsonDataSpamKey}"": ""{JsonDataSpamValue}""
}}";
    public const string JsonDataDiscardKey = "secretInfo";
    public const string JsonDataDiscard = $@"{{
    ""userName"": ""gamer"",
    ""password"": ""password"",
    ""{JsonDataDiscardKey}"": {{
        ""cardNumber"": 1234123412341234,
        ""hondaCivic"": 676,
        ""expiry"": ""6/7"",
        ""valid"": false,
        ""transactions"": [{{ ""date"": 453461254869, ""id"": ""jorjor well"" }}, {{ ""date"": 0, ""id"": null }}]
    }},
    ""stats"": {{
        ""score"": 99999999999,
        ""moneysWasted"": ""9000.24 USD"",
        ""lifetime"": 12000,
        ""skill"": 21498124
    }},
    ""badAtThisGame"": true
}}";
    // Discarded values are validated and parsed actually, they are just "skipped until the next adjacent value"
    public const string JsonDataDiscardInvalid = $@"{{
    ""userName"": ""gamer"",
    ""password"": ""password"",
    ""{JsonDataDiscardKey}"": {{
        ""cardNumber"": 1234123412341234,
        ""hondaCivic"": 676,
        ""transactions"": [{{ 67984gjkdsngjkfhn ewrg,,jkhgku.jshf.h ""addawdws"" ,,:: // }}]
    }},
    ""stats"": {{
        ""score"": 99999999999,
        ""moneysWasted"": ""9000.24 USD"",
        ""lifetime"": 12000,
        ""skill"": 21498124
    }},
    ""badAtThisGame"": true
}}";
    // ↓ the sj.h does not complain what was there before, "if it starts/ends a recursive object it was valid". this one will complain though.
    public const string JsonDataStackingInvalid = @"{
    ""key"": { ""another key"": [{]}, ]
}";

    public const string JsonDataComment = @"/* Let's start our ""invalid"" JSON journey! */
// Also some more lines.
{
        // I like annotating my JSON, but it gets deleted when it's serialized :(
        /* wen eta fix this? */
        ""Array"": [1,2,3,4,5,6,7,8, /* oops convenient comment */ 9,10,11],
        ""Objects"": { ""Yes"": true, ""No"": false, ""Empty??"": null, ""Whatever"": ""/* Not really a comment. */"" } // I like putting comments where inconvenient
} // Our line ends with */";
    public const string JsonDataCommentInvalid = @"/* Let's start our invalid JSON journey! */
// Also some more lines.
{
        // I like annotating my JSON, but it gets deleted when it's serialized :(
        /* wen eta fix this?
} // Our line ends with */";

    // - File Data
    // * "valid.json" is from some random website.
    // * JSON data sets are from https://microsoftedge.github.io/Demos/json-dummy-data/
    public static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string JsonFileValidName = Path.Combine(BaseDirectory, "TestFiles", "valid.json"),
        JsonFileLargeName = Path.Combine(BaseDirectory, "TestFiles", "5mb.json"),
        JsonFileLargeMinName = Path.Combine(BaseDirectory, "TestFiles", "5mb.min.json"),
        JsonFileInvalidUnterminated = Path.Combine(BaseDirectory, "TestFiles", "unterminated.json"),
        JsonFileInvalidNoColon = Path.Combine(BaseDirectory, "TestFiles", "missing_colon.json"),
        JsonFileInvalidBinary = Path.Combine(BaseDirectory, "TestFiles", "binary.json");

    // - Escape Data
    public const string EscapeContent = "Quote: \", Backslash: \\, Tab: \t, Newline: \n, Pizza: \uD83C\uDF55, The 🅱 variant: \uD83C\uDD71\uFE0F";
    public const string EscapeBrokenUnescape = "Oh wow, I'm escaping things I shouldn't! : \\a\\b\\c\\d \\e\\f\\g\\h\\i\\j\\k \\l\\m\\n\\o\\p\\q\\r\\s\\ t\\u\\v\\w\\x\\y\\z\\A\\B\\C \\D\\E\\F\\G\\H\\I\\J\\K\\L\\M\\N\\O\\P\\Q\\ R\\S\\T\\U\\V \\W\\X \\Y\\Z'";
    public const string EscapeBrokenSurrogate = "\uDF55\uD83C\uDD71\uD83C\uFE0F In C++, surrogate escape you!";
}
