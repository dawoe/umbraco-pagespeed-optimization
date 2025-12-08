# umbraco-pagespeed-optimization

This Umbraco package improves your Google page speed score by optionally applying :

- Static assets cache

- Image optimizations

- Response compression.

After installing the package through nuget the following settings can be added to the appsettings.json to control the behavior of the package.

```json
{
  "Umbraco": {
        "Community": {
        "PageSpeedOptimizer": {
            "StaticAssetsCache": {
                "Enabled": false,
                "MaxAgeInDays" : 365,
                "DefaultCacheExtensions": ["js", "css", "svg", "woff2", "woff", "otf", "ttf", "ico", "jpg", "png", "gif", "webp"]
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
