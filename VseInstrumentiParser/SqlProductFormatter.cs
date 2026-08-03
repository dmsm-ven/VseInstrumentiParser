using System.Globalization;
using System.Text;
using System.Web;

namespace VseInstrumentiParser;

public class SqlProductFormatter()
{
    public string GenerateSqlForLastParsedProduct(ViParser parser, string manufacturerFolder)
    {
        var sb = new StringBuilder();

        string desc = HttpUtility.HtmlEncode(parser.LastParseDescriptionHtml);

        //Описание
        sb.AppendLine($"UPDATE oc_product_description SET description = '{desc}' WHERE product_id = (SELECT product_id FROM oc_product WHERE model = '{parser.LastModelData}' OR sku = '{parser.LastModelData}' LIMIT 1);");

        //Изображения
        if (parser.LastImagesData.Length > 0)
        {
            sb.AppendLine($"UPDATE oc_product SET image = '{manufacturerFolder}/{parser.LastImagesData[0].Replace("\\", "_")}' WHERE model = '{parser.LastModelData}' OR sku = '{parser.LastModelData}';");
            if (parser.LastImagesData.Length > 1)
            {
                sb.AppendLine("INSERT INTO oc_product_image (product_id, image, sort_order) VALUES");
                int sort_order = 0;
                foreach (var image in parser.LastImagesData.Skip(1))
                {
                    sb.AppendLine($"((SELECT product_id FROM oc_product WHERE model = '{parser.LastModelData}' OR sku = '{parser.LastModelData}'), '{manufacturerFolder}/{image.Replace("\\", "_")}', {sort_order++}),");
                }
                sb.Remove(sb.Length - 3, 3);
                sb.AppendLine(";");
            }
        }

        //Габариты
        sb.AppendLine($@"UPDATE oc_product SET weight = '{parser.LastDimensionsData.WeightKg.ToString(new CultureInfo("en-EN"))}',
                                                length = '{parser.LastDimensionsData.LengthMm.ToString(new CultureInfo("en-EN"))}',
                                                width = '{parser.LastDimensionsData.WidthMm.ToString(new CultureInfo("en-EN"))}',
                                                height = '{parser.LastDimensionsData.HeightMm.ToString(new CultureInfo("en-EN"))}' 
                        WHERE model = '{parser.LastModelData}' OR sku = '{parser.LastModelData}';");

        return sb.ToString();
    }
}
