using System.Collections.Generic;

namespace Hjemat
{
    public class ProductValue
    {
        public byte id;
        public string name;
        public string description;
        public string type;

        public ProductValue()
        {

        }
    }

    public class Product
    {
        public int id;
        public string name;
        public string description;
        public List<ProductValue> values = new List<ProductValue>();

        public Product(int id = 0, string name = "ProductName",
					   string description = "A product", List<ProductValue> values = null)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.values = values ?? new List<ProductValue>();
        }
    }
}

