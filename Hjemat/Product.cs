using System.Collections.Generic;

namespace Hjemat
{
    public class ProductValue
    {
        public string name;
        public string description;

        public ProductValue()
        {

        }
    }

    public class Product
    {
        public int productID;
        public string name;
        public string description;
        public Dictionary<byte, ProductValue> values = new Dictionary<byte, ProductValue>();

        public Product(int productID = 0, string name = "ProductName",
					   string description = "A product", Dictionary<byte, ProductValue> values = null)
        {
            this.productID = productID;
            this.name = name;
            this.description = description;
            this.values = values ?? new Dictionary<byte, ProductValue>();
        }
    }
}

