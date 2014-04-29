/*
 * Copyright © 2014 Ryan Bray
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TextureManager
{
  class CacheController
  {
    String MD5String = "";
    String LastFile = "";
    public static CacheController instance = null;

    public GameDatabase.TextureInfo FetchCacheTexture(TexInfo Texture, bool compress, bool mipmaps,
                                                      bool makeNotReadable)
    {
      String textureName = Texture.name;
      String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + textureName;
      String cacheFile = Util.PATH + "Cache/" + textureName;
      String cacheConfigFile = cacheFile + ".tcache";
      cacheFile += ".pngcache";
      if (File.Exists(cacheConfigFile))
      {
        ConfigNode config = ConfigNode.Load(cacheConfigFile);
        string format = config.GetValue("orig_format");
        String cacheHash = config.GetValue("md5");
        int origWidth, origHeight;
        string origWidthString = config.GetValue("orig_width");
        string origHeightString = config.GetValue("orig_height");
        int.TryParse(origWidthString, out origWidth);
        int.TryParse(origHeightString, out origHeight);

        if (origWidthString == null || origHeightString == null ||
            cacheHash == null || format == null)
        {
          return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
        }

        originalTextureFile += format;
        String hashString = GetMD5String(originalTextureFile);

        Texture.Resize(origWidth, origHeight);

        if (format != null && File.Exists(originalTextureFile) && File.Exists(cacheFile))
        {

          String cacheIsNormString = config.GetValue("is_normal");
          String cacheWidthString = config.GetValue("width");
          String cacheHeihtString = config.GetValue("height");
          bool cacheIsNorm = false;
          int cacheWidth = 0;
          int cacheHeight = 0;
          bool.TryParse(cacheIsNormString, out cacheIsNorm);
          int.TryParse(cacheWidthString, out cacheWidth);
          int.TryParse(cacheHeihtString, out cacheHeight);

          if (cacheHash != hashString || cacheIsNorm != Texture.isNormalMap
              || Texture.resizeWidth != cacheWidth || Texture.resizeHeight != cacheHeight)
          {
            if (cacheHash != hashString)
            {
              Util.debugLog(cacheHash + " != " + hashString);
            }
            if (cacheIsNorm != Texture.isNormalMap)
            {
              Util.debugLog(cacheIsNorm + " != " + Texture.isNormalMap);
            }
            if (Texture.resizeWidth != cacheWidth)
            {
              Util.debugLog(Texture.resizeWidth + " != " + cacheWidth);
            }
            if (Texture.resizeHeight != cacheHeight)
            {
              Util.debugLog(Texture.resizeHeight + " != " + cacheHeight);
            }
            return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
          }
          else if (cacheHash == hashString && !Texture.needsResize)
          {
            return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
          }
          else
          {
            Util.debugLog("Loading from cache... " + textureName);
            Texture.needsResize = false;
            Texture2D newTex = new Texture2D(4, 4);
            GameDatabase.TextureInfo cacheTexture =
              new GameDatabase.TextureInfo(newTex, Texture.isNormalMap, !makeNotReadable, compress);
            Texture.texture = cacheTexture;
            Texture.filename = cacheFile;
            Converter.instance.IMGToTexture(Texture, mipmaps, cacheIsNorm);
            cacheTexture.name = textureName;
            newTex.name = textureName;
            if (compress)
            {
              newTex.Compress(true);
            }
            newTex.Apply(mipmaps, makeNotReadable);
            return cacheTexture;
          }
        }
        else
        {
          return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
        }
      }
      else
      {
        return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
      }

    }

    private GameDatabase.TextureInfo RebuildCache(TexInfo Texture, bool compress, bool mipmaps,
                                                  bool makeNotReadable)
    {
      Texture.loadOriginalFirst = true;
      Util.debugLog("Loading texture...");
      Converter.instance.GetReadable(Texture, mipmaps);
      Util.debugLog("Texture loaded.");

      GameDatabase.TextureInfo cacheTexture = Texture.texture;
      Texture2D tex = cacheTexture.texture;

      String textureName = cacheTexture.name;
      String cacheFile = Util.PATH + "Cache/" + textureName;
      if (Texture.needsResize)
      {
        Util.debugLog("Rebuilding Cache... " + Texture.name);

        Util.debugLog("Saving cache file " + cacheFile + ".pngcache");
        Converter.WriteTo(cacheTexture.texture, cacheFile + ".pngcache");

        String originalTextureFile = Texture.filename;
        String cacheConfigFile = cacheFile + ".tcache";
        Util.debugLog("Created Config for" + originalTextureFile);

        String hashString = GetMD5String(originalTextureFile);

        ConfigNode config = new ConfigNode();
        config.AddValue("md5", hashString);
        Util.debugLog("md5: " + hashString);
        config.AddValue("orig_format", Path.GetExtension(originalTextureFile));
        Util.debugLog("orig_format: " + Path.GetExtension(originalTextureFile));
        config.AddValue("orig_width", Texture.width.ToString());
        Util.debugLog("orig_width: " + Texture.width.ToString());
        config.AddValue("orig_height", Texture.height.ToString());
        Util.debugLog("orig_height: " + Texture.height.ToString());
        config.AddValue("is_normal", cacheTexture.isNormalMap.ToString());
        Util.debugLog("is_normal: " + cacheTexture.isNormalMap.ToString());
        config.AddValue("width", Texture.resizeWidth.ToString());
        Util.debugLog("width: " + Texture.resizeWidth.ToString());
        config.AddValue("height", Texture.resizeHeight.ToString());
        Util.debugLog("height: " + Texture.resizeHeight.ToString());

        config.Save(cacheConfigFile);
        Util.debugLog("Saved Config.");
      }
      else
      {
        String directory = Path.GetDirectoryName(cacheFile + ".none");
        if (File.Exists(directory))
        {
          File.Delete(directory);
        }
        Directory.CreateDirectory(directory);
      }

      if (compress)
      {
        tex.Compress(true);
      }
      cacheTexture.isCompressed = compress;
      if (!makeNotReadable)
      {
        tex.Apply(mipmaps);
      }
      else
      {
        tex.Apply(mipmaps, true);
      }
      cacheTexture.isReadable = !makeNotReadable;

      return cacheTexture;
    }

    String GetMD5String(String file)
    {
      if (file == LastFile)
      {
        return MD5String;
      }
      if (File.Exists(file))
      {
        FileStream stream = File.OpenRead(file);
        MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(stream);
        stream.Close();
        MD5String = BitConverter.ToString(hash);
        LastFile = file;
        return MD5String;
      }
      else
      {
        return null;
      }
    }

    public static int MemorySaved(int originalWidth, int originalHeight, TextureFormat originalFormat,
                                  bool originalMipmaps, GameDatabase.TextureInfo Texture)
    {
      int width = Texture.texture.width;
      int height = Texture.texture.height;
      TextureFormat format = Texture.texture.format;
      bool mipmaps = Texture.texture.mipmapCount == 1 ? false : true;
      Util.debugLog("Texture: " + Texture.name);
      Util.debugLog("is normalmap: " + Texture.isNormalMap);
      Texture2D tex = Texture.texture;
      Util.debugLog("originalWidth: " + originalWidth);
      Util.debugLog("originalHeight: " + originalHeight);
      Util.debugLog("originalFormat: " + originalFormat);
      Util.debugLog("originalMipmaps: " + originalMipmaps);
      Util.debugLog("width: " + width);
      Util.debugLog("height: " + height);
      Util.debugLog("format: " + format);
      Util.debugLog("mipmaps: " + mipmaps);
      bool readable = true;
      try
      {
        tex.GetPixel(0, 0);
      }
      catch
      {
        readable = false;
      }
      ;
      Util.debugLog("readable: " + readable);
      if (readable != Texture.isReadable)
      {
        Util.debugLog("Readbility does not match!");
      }
      int oldSize = 0;
      int newSize = 0;
      switch (originalFormat)
      {
        case TextureFormat.ARGB32:
        case TextureFormat.RGBA32:
        case TextureFormat.BGRA32:
        {
          oldSize = 4 * (originalWidth * originalHeight);
          break;
        }
        case TextureFormat.RGB24:
        {
          oldSize = 3 * (originalWidth * originalHeight);
          break;
        }
        case TextureFormat.Alpha8:
        {
          oldSize = originalWidth * originalHeight;
          break;
        }
        case TextureFormat.DXT1:
        {
          oldSize = (originalWidth * originalHeight) / 2;
          break;
        }
        case TextureFormat.DXT5:
        {
          oldSize = originalWidth * originalHeight;
          break;
        }
      }
      switch (format)
      {
        case TextureFormat.ARGB32:
        case TextureFormat.RGBA32:
        case TextureFormat.BGRA32:
        {
          newSize = 4 * (width * height);
          break;
        }
        case TextureFormat.RGB24:
        {
          newSize = 3 * (width * height);
          break;
        }
        case TextureFormat.Alpha8:
        {
          newSize = width * height;
          break;
        }
        case TextureFormat.DXT1:
        {
          newSize = (width * height) / 2;
          break;
        }
        case TextureFormat.DXT5:
        {
          newSize = width * height;
          break;
        }
      }
      if (originalMipmaps)
      {
        oldSize += (int) (oldSize * .33f);
      }
      if (mipmaps)
      {
        newSize += (int) (newSize * .33f);
      }
      return (oldSize - newSize);
    }
  }
}
