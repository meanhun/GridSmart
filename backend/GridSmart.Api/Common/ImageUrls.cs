using GridSmart.Api.Data;

namespace GridSmart.Api.Common;

public static class ImageUrls
{
    /// <summary>Anh thu nho luu canh anh goc: abc.jpg -> abc_thumb.jpg.</summary>
    public static string Thumbnail(string url)
    {
        var dot = url.LastIndexOf('.');
        return dot < 0 ? url + "_thumb" : url[..dot] + "_thumb" + url[dot..];
    }

    /// <summary>Chuyen mot dong tbl_incident_images (Row) sang DTO.</summary>
    public static ImageDto ToDto(Row i)
    {
        var url = i.GetString("image_url");
        var point = IncidentRows.ParsePoint(i.GetStringOrNull("capture_point_wkt"));
        return new ImageDto(i.GetLong("id"), i.GetString("image_type"), url, Thumbnail(url),
            point?.Y, point?.X, i.GetDateTime("uploaded_at"));
    }
}
