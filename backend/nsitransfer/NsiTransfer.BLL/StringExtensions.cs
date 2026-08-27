namespace NsiTransfer.BLL;

public static class StringNumberExtensions
{
    public static string Increment(this string number)
    {
        if (string.IsNullOrEmpty(number))
            return "1";

        char[] chars = number.ToCharArray();
        int carry = 1;

        for (int i = chars.Length - 1; i >= 0 && carry > 0; i--)
        {
            int digit = chars[i] - '0' + carry;
            if (digit >= 10)
            {
                chars[i] = '0';
                carry = 1;
            }
            else
            {
                chars[i] = (char)('0' + digit);
                carry = 0;
            }
        }

        return carry > 0 ? "1" + new string(chars) : new string(chars);
    }
}
