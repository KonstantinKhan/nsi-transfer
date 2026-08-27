namespace NsiTransfer.BLL.Tools;

public class NumericStringComparer : IComparer<string>
{
    public static readonly NumericStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // Пропускаем ведущие нули
        int startX = SkipLeadingZeros(x);
        int startY = SkipLeadingZeros(y);

        int lenX = x.Length - startX;
        int lenY = y.Length - startY;

        // Если после удаления нулей длины разные — более длинное число больше
        if (lenX != lenY)
            return lenX.CompareTo(lenY);

        // Длины равны — сравниваем посимвольно
        for (int i = 0; i < lenX; i++)
        {
            if (x[startX + i] != y[startY + i])
                return x[startX + i].CompareTo(y[startY + i]);
        }

        return 0;
    }

    private static int SkipLeadingZeros(string s)
    {
        int i = 0;
        while (i < s.Length - 1 && s[i] == '0')
            i++;
        return i;
    }
}
