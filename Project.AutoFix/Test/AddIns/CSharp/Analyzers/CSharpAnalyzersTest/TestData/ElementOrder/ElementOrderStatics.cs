namespace ElementOrderStatics1
{
    public class Class1
    {
        // Correct order.
        public static int staticField1;

        public int staticField1;
    }

    public class Class2
    {
        // Incorrect order.
        public int staticField1;

        public static int staticField1;
    }

    public class Class3
    {
        // Correct order.
        public static Class3()
        {
        }

        public Class3()
        {
        }
    }

    public class Class4
    {
        // Incorrect order.
        public Class4()
        {
        }

        public static Class4()
        {
        }
    }

    public class Class5
    {
        // Correct order.
        public static bool Property1
        {
            get { return true; }
        }

        public bool Property2
        {
            get { return true; }
        }
    }

    public class Class6
    {
        // Incorrect order.
        public bool Property1
        {
            get { return true; }
        }

        public static bool Property2
        {
            get { return true; }
        }
    }

    public class Class7
    {
        // Correct order.
        public static bool Method1()
        {
            return true;
        }

        public bool Method2()
        {
            return true;
        }
    }

    public class Class8
    {
        // Incorrect order.
        public bool Method1()
        {
            return true;
        }

        public static bool Method2()
        {
            return true;
        }
    }
}