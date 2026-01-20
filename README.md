# Umbraco PageSpeed Optimizer

This Umbraco package improves your Google page speed score by optionally applying :

- Static assets cache

- Image optimizations

- Response compression.

After installing the package through nuget the following settings can be added to the appsettings.json to control the behavior of the package. Below you will find a example with default values.

```json
{
  "Umbraco": {
        "Community": {
        "PageSpeedOptimizer": {
            "StaticAssetsCache": {
                "Enabled": false,
                "MaxAgeInDays" : 365,
                "DefaultCacheExtensions": ["js", "css", "svg", "woff2", "woff", "otf", "ttf", "ico", "jpg", "png", "gif", "webp"],
                "CacheBackOffice": false,
                "MaxAgeInDaysForBackOffice": 7,
            },
            "ResponseCompression": {
                "Enabled": false
            },
            "ImageOptimization": {
                "Enabled": false,
                "DefaultImageQuality": 85,
                "ForceWebP": false
            }
        }
    }
  }
}
```

If you enable ResponseCompression the following line needs to be added to your Program.cs before calling `await app.BootUmbracoAsync();`

```csharp
app.EnableResponseCompression();
```

## Appsettings

### StaticAssetsCache

- Enabled : enables caching of static assets by setting the "cache-control" header

- MaxAgeInDays : the amount of days that the browser can cache the file

- DefaultCacheExtensions : the file extensions the cache-control header gets applied to.

- CacheBackOffice : enables setting the "cache-control" for backoffice assets

- MaxAgeInDaysForBackOffice: the amount of days that the browser can cache the backoffice files.

### ResponseCompresion

- Enabled : enables gzip or brotli compression. If your webserver already handles this, you do not need to enable this

### ImageOptimization

- Enabled : enables optimizing of images.

- DefaultImageQuality : this is the quality that get's applied to all images. If you pass the a quality to a crop url this will be used instead of the default quality.

- ForceWebP : set this to true to serve all images in webp format

# 
