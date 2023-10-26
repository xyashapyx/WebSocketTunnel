namespace WebSocketTunnel;

public static class IntArrayConvertor
{
    private static int NumDigits(int n) {
        if (n < 0) {
            n = (n == Int32.MinValue) ? Int32.MaxValue : -n;
        }
        if (n < 10) return 1;
        if (n < 100) return 2;
        if (n < 1000) return 3;
        if (n < 10000) return 4;
        if (n < 100000) return 5;
        if (n < 1000000) return 6;
        if (n < 10000000) return 7;
        if (n < 100000000) return 8;
        if (n < 1000000000) return 9;
        return 10;
    }

    public static byte[] IntToArr(int n)
    {
        var result = new Byte[NumDigits(n)];
        for (int i = result.Length - 1; i >= 0; i--) {
            result[i] = (byte)(n % 10);
            n /= 10;
        }
        return result;
    }
    
    public static int ArrToInt(Span<byte> digits)
    {
        int result = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            result += (int)(digits[i] * Math.Pow(10, digits.Length - i - 1));
        }

        return result;
    }
}