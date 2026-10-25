namespace N3DS.Save;

public static class Misc
{
    public static int AlignUp(int value, int align) => value + (align - value % align) % align;

    public static int DivideUp(int value, int align)
    {
        if (value == 0) 
            return 0;

        return 1 + (value - 1) / align;
    }
}