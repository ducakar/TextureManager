﻿/*
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
using System.Text;
using UnityEngine;

namespace TextureManager
{
  class Converter
  {
    const int MAX_IMAGE_SIZE = 4096 * 4096 * 5;
    byte[] imageBuffer = null;
    public static Converter instance = null;

    public void InitImageBuffer()
    {
      if (imageBuffer == null)
      {
        imageBuffer = new byte[MAX_IMAGE_SIZE];
      }
    }

    public void DestroyImageBuffer()
    {
      imageBuffer = null;
    }

    public static void Resize(GameDatabase.TextureInfo texture, int width, int height, bool mipmaps,
                              bool convertToNormalFormat)
    {
      Util.debugLog("Resizing...");
      Texture2D tex = texture.texture;
      TextureFormat format = tex.format;
      if (texture.isNormalMap)
      {
        format = TextureFormat.ARGB32;
      }
      else if (format == TextureFormat.DXT1 || format == TextureFormat.RGB24)
      {
        format = TextureFormat.RGB24;
      }
      else
      {
        format = TextureFormat.RGBA32;
      }

      Color32[] pixels = tex.GetPixels32();
      if (convertToNormalFormat)
      {
        ConvertToUnityNormalMap(pixels);
      }

      Color32[] newPixels = ResizePixels(pixels, tex.width, tex.height, width, height);
      tex.Resize(width, height, format, mipmaps);
      tex.SetPixels32(newPixels);
      tex.Apply(mipmaps);
    }

    private static Color32[] ResizePixels(Color32[] pixels, int width, int height,
                                          int newWidth, int newHeight)
    {
      Color32[] newPixels = new Color32[newWidth * newHeight];
      int index = 0;
      for (int h = 0; h < newHeight; h++)
      {
        for (int w = 0; w < newWidth; w++)
        {
          newPixels[index++] = GetPixel(pixels, width, height,
                                        ((float) w) / newWidth, ((float) h) / newHeight,
                                        newWidth, newHeight);
        }
      }
      return newPixels;
    }

    public static void ConvertToUnityNormalMap(Color32[] colors)
    {
      for (int i = 0; i < colors.Length; i++)
      {
        colors[i].a = colors[i].r;
        colors[i].r = colors[i].g;
        colors[i].b = colors[i].g;
      }
    }

    private static Color32 GetPixel(Color32[] pixels, int width, int height, float w, float h,
                                    int newWidth, int newHeight)
    {
      float widthDist = 4.0f - ((4.0f * (float) newWidth) / width);
      float heightDist = 4.0f - ((4.0f * (float) newHeight) / height);
      int[,] posArray = new int[2, 4];
      posArray[0, 0] = (int) Math.Floor((w * width) - widthDist);
      posArray[0, 1] = (int) Math.Floor(w * width);
      posArray[0, 2] = (int) Math.Ceiling((w * width) + widthDist);
      posArray[0, 3] = (int) Math.Ceiling((w * width) + (2.0 * widthDist));
      posArray[1, 0] = (int) Math.Floor((h * height) - heightDist);
      posArray[1, 1] = (int) Math.Floor(h * height);
      posArray[1, 2] = (int) Math.Ceiling((h * height) + heightDist);
      posArray[1, 3] = (int) Math.Ceiling((h * height) + (2.0 * heightDist));

      Color32 cw1 = new Color32(), cw2 = new Color32(), cw3 = new Color32(), cw4 = new Color32();
      Color32 ch1 = new Color32(), ch2 = new Color32(), ch3 = new Color32(), ch4 = new Color32();
      int w1 = posArray[0, 0];
      int w2 = posArray[0, 1];
      int w3 = posArray[0, 2];
      int w4 = posArray[0, 3];
      int h1 = posArray[1, 0];
      int h2 = posArray[1, 1];
      int h3 = posArray[1, 2];
      int h4 = posArray[1, 3];

      if (h2 >= 0 && h2 < height)
      {
        if (w2 >= 0 && w2 < width)
        {
          cw2 = pixels[w2 + (h2 * width)];
        }
        if (w1 >= 0 && w1 < width)
        {
          cw1 = pixels[w1 + (h2 * width)];
        }
        else
        {
          cw1 = cw2;
        }
        if (w3 >= 0 && w3 < width)
        {
          cw3 = pixels[w3 + (h2 * width)];
        }
        else
        {
          cw3 = cw2;
        }
        if (w4 >= 0 && w4 < width)
        {
          cw4 = pixels[w4 + (h2 * width)];
        }
        else
        {
          cw4 = cw3;
        }
      }
      if (w2 >= 0 && w2 < width)
      {
        if (h2 >= 0 && h2 < height)
        {
          ch2 = pixels[w2 + (h2 * width)];
        }
        if (h1 >= 0 && h1 < height)
        {
          ch1 = pixels[w2 + (h1 * width)];
        }
        else
        {
          ch1 = ch2;
        }
        if (h3 >= 0 && h3 < height)
        {
          ch3 = pixels[w2 + (h3 * width)];
        }
        else
        {
          ch3 = ch2;
        }
        if (h4 >= 0 && h4 < height)
        {
          ch4 = pixels[w2 + (h4 * width)];
        }
        else
        {
          ch4 = ch3;
        }
      }
      byte cwr = (byte) (((.25f * cw1.r) + (.75f * cw2.r) + (.75f * cw3.r) + (.25f * cw4.r)) / 2.0f);
      byte cwg = (byte) (((.25f * cw1.g) + (.75f * cw2.g) + (.75f * cw3.g) + (.25f * cw4.g)) / 2.0f);
      byte cwb = (byte) (((.25f * cw1.b) + (.75f * cw2.b) + (.75f * cw3.b) + (.25f * cw4.b)) / 2.0f);
      byte cwa = (byte) (((.25f * cw1.a) + (.75f * cw2.a) + (.75f * cw3.a) + (.25f * cw4.a)) / 2.0f);
      byte chr = (byte) (((.25f * ch1.r) + (.75f * ch2.r) + (.75f * ch3.r) + (.25f * ch4.r)) / 2.0f);
      byte chg = (byte) (((.25f * ch1.g) + (.75f * ch2.g) + (.75f * ch3.g) + (.25f * ch4.g)) / 2.0f);
      byte chb = (byte) (((.25f * ch1.b) + (.75f * ch2.b) + (.75f * ch3.b) + (.25f * ch4.b)) / 2.0f);
      byte cha = (byte) (((.25f * ch1.a) + (.75f * ch2.a) + (.75f * ch3.a) + (.25f * ch4.a)) / 2.0f);
      byte R = (byte) ((cwr + chr) / 2.0f);
      byte G = (byte) ((cwg + chg) / 2.0f);
      byte B = (byte) ((cwb + chb) / 2.0f);
      byte A = (byte) ((cwa + cha) / 2.0f);

      Color32 color = new Color32(R, G, B, A);
      return color;
    }

    public void MBMToTexture(TexInfo Texture, bool mipmaps)
    {
      GameDatabase.TextureInfo texture = Texture.texture;
      InitImageBuffer();
      FileStream mbmStream = new FileStream(Texture.filename, FileMode.Open, FileAccess.Read);
      mbmStream.Position = 4;

      uint width = 0, height = 0;
      for (int b = 0; b < 4; b++)
      {
        width >>= 8;
        width |= (uint) (mbmStream.ReadByte() << 24);
      }
      for (int b = 0; b < 4; b++)
      {
        height >>= 8;
        height |= (uint) (mbmStream.ReadByte() << 24);
      }
      mbmStream.Position = 12;
      bool convertToNormalFormat = false;
      if (mbmStream.ReadByte() == 1)
      {
        texture.isNormalMap = true;
      }
      else
      {
        convertToNormalFormat = texture.isNormalMap ? true : false;
      }

      mbmStream.Position = 16;
      int format = mbmStream.ReadByte();
      mbmStream.Position += 3;

      int imageSize = (int) (width * height * 3);
      TextureFormat texformat = TextureFormat.RGB24;
      bool alpha = false;
      if (format == 32)
      {
        imageSize += (int) (width * height);
        texformat = TextureFormat.ARGB32;
        alpha = true;
      }
      if (texture.isNormalMap)
      {
        texformat = TextureFormat.ARGB32;
      }

      mbmStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
      mbmStream.Close();

      Texture2D tex = texture.texture;

      Color32[] colors = new Color32[width * height];
      int n = 0;
      for (int i = 0; i < width * height; i++)
      {
        colors[i].r = imageBuffer[n++];
        colors[i].g = imageBuffer[n++];
        colors[i].b = imageBuffer[n++];
        if (alpha)
        {
          colors[i].a = imageBuffer[n++];
        }
        else
        {
          colors[i].a = 255;
        }
        if (convertToNormalFormat)
        {
          colors[i].a = colors[i].r;
          colors[i].r = colors[i].g;
          colors[i].b = colors[i].g;
        }
      }

      if (Texture.loadOriginalFirst)
      {
        Texture.Resize((int) width, (int) height);
      }

      if (Texture.needsResize)
      {
        colors = Converter.ResizePixels(colors,
                                        (int) width,
                                        (int) height,
                                        Texture.resizeWidth,
                                        Texture.resizeHeight);
        width = (uint) Texture.resizeWidth;
        height = (uint) Texture.resizeHeight;
      }
      tex.Resize((int) width, (int) height, texformat, mipmaps);
      tex.SetPixels32(colors);
      tex.Apply(mipmaps, false);
    }

    public void IMGToTexture(TexInfo Texture, bool mipmaps, bool isNormalFormat)
    {
      GameDatabase.TextureInfo texture = Texture.texture;
      InitImageBuffer();
      FileStream imgStream = new FileStream(Texture.filename, FileMode.Open, FileAccess.Read);
      imgStream.Position = 0;
      imgStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
      imgStream.Close();

      Texture2D tex = texture.texture;
      tex.mipMapBias = 0;
      tex.LoadImage(imageBuffer);
      bool convertToNormalFormat = texture.isNormalMap && !isNormalFormat ? true : false;
      bool hasMipmaps = tex.mipmapCount == 1 ? false : true;
      if (Texture.loadOriginalFirst)
      {
        Texture.Resize(tex.width, tex.height);
      }
      TextureFormat format = tex.format;
      if (texture.isNormalMap)
      {
        format = TextureFormat.ARGB32;
      }
      if (Texture.needsResize)
      {
        Resize(texture, Texture.resizeWidth, Texture.resizeHeight, mipmaps, convertToNormalFormat);
      }
      else if (convertToNormalFormat || hasMipmaps != mipmaps || format != tex.format)
      {
        Color32[] pixels = tex.GetPixels32();
        if (convertToNormalFormat)
        {
          for (int i = 0; i < pixels.Length; i++)
          {
            pixels[i].a = pixels[i].r;
            pixels[i].r = pixels[i].g;
            pixels[i].b = pixels[i].g;
          }
        }
        if (tex.format != format || hasMipmaps != mipmaps)
        {
          tex.Resize(tex.width, tex.height, format, mipmaps);
        }
        tex.SetPixels32(pixels);
        tex.Apply(mipmaps);
      }

    }

    public void TGAToTexture(TexInfo Texture, bool mipmaps)
    {
      GameDatabase.TextureInfo texture = Texture.texture;
      InitImageBuffer();
      FileStream tgaStream = new FileStream(Texture.filename, FileMode.Open, FileAccess.Read);
      tgaStream.Position = 0;
      tgaStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
      tgaStream.Close();

      byte imgType = imageBuffer[2];
      int width = imageBuffer[12] | (imageBuffer[13] << 8);
      int height = imageBuffer[14] | (imageBuffer[15] << 8);
      if (Texture.loadOriginalFirst)
      {
        Texture.Resize(width, height);
      }
      int depth = imageBuffer[16];
      bool alpha = depth == 32 ? true : false;
      TextureFormat texFormat = depth == 32 ? TextureFormat.RGBA32 : TextureFormat.RGB24;
      if (texture.isNormalMap)
      {
        texFormat = TextureFormat.ARGB32;
      }
      bool convertToNormalFormat = texture.isNormalMap ? true : false;

      Texture2D tex = texture.texture;

      Color32[] colors = new Color32[width * height];
      int n = 18;
      if (imgType == 2)
      {
        for (int i = 0; i < width * height; i++)
        {
          colors[i].b = imageBuffer[n++];
          colors[i].g = imageBuffer[n++];
          colors[i].r = imageBuffer[n++];
          if (alpha)
          {
            colors[i].a = imageBuffer[n++];
          }
          else
          {
            colors[i].a = 255;
          }
          if (convertToNormalFormat)
          {
            colors[i].a = colors[i].r;
            colors[i].r = colors[i].g;
            colors[i].b = colors[i].g;
          }
        }
      }
      else if (imgType == 10)
      {
        int i = 0;
        int run = 0;
        while (i < width * height)
        {
          run = imageBuffer[n++];
          if ((run & 0x80) != 0)
          {
            run = (run ^ 0x80) + 1;
            colors[i].b = imageBuffer[n++];
            colors[i].g = imageBuffer[n++];
            colors[i].r = imageBuffer[n++];
            if (alpha)
            {
              colors[i].a = imageBuffer[n++];
            }
            else
            {
              colors[i].a = 255;
            }
            if (convertToNormalFormat)
            {
              colors[i].a = colors[i].r;
              colors[i].r = colors[i].g;
              colors[i].b = colors[i].g;
            }
            i++;
            for (int c = 1; c < run; c++, i++)
            {
              colors[i] = colors[i - 1];
            }
          }
          else
          {
            run += 1;
            for (int c = 0; c < run; c++, i++)
            {
              colors[i].b = imageBuffer[n++];
              colors[i].g = imageBuffer[n++];
              colors[i].r = imageBuffer[n++];
              if (alpha)
              {
                colors[i].a = imageBuffer[n++];
              }
              else
              {
                colors[i].a = 255;
              }
              if (convertToNormalFormat)
              {
                colors[i].a = colors[i].r;
                colors[i].r = colors[i].g;
                colors[i].b = colors[i].g;
              }
            }
          }
        }
      }
      else
      {
        Util.debugLog("TGA format is not supported!");
      }

      if (Texture.needsResize)
      {
        colors = Converter.ResizePixels(colors,
                                        width,
                                        height,
                                        Texture.resizeWidth,
                                        Texture.resizeHeight);
        width = Texture.resizeWidth;
        height = Texture.resizeHeight;
      }
      tex.Resize((int) width, (int) height, texFormat, mipmaps);
      tex.SetPixels32(colors);
      tex.Apply(mipmaps, false);
    }

    public void GetReadable(TexInfo Texture, bool mipmaps)
    {
      String mbmPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".mbm";
      String pngPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".png";
      String jpgPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".jpg";
      String tgaPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".tga";
      if (File.Exists(pngPath) || File.Exists(jpgPath) || File.Exists(tgaPath) || File.Exists(mbmPath))
      {
        Texture2D tex = new Texture2D(2, 2);
//                String name;
//                if (Texture.name.Length > 0)
//                {
//                    name = Texture.name;
//                }
//                else
//                {
//                    name = Texture.name;
//                }

        GameDatabase.TextureInfo newTexture = new GameDatabase.TextureInfo(tex, Texture.isNormalMap,
                                                                           true, false);
        Texture.texture = newTexture;
        newTexture.name = Texture.name;
        if (File.Exists(pngPath))
        {
          Texture.filename = pngPath;
          IMGToTexture(Texture, mipmaps, false);
        }
        else if (File.Exists(jpgPath))
        {
          Texture.filename = jpgPath;
          IMGToTexture(Texture, mipmaps, false);
        }
        else if (File.Exists(tgaPath))
        {
          Texture.filename = tgaPath;
          TGAToTexture(Texture, mipmaps);
        }
        else if (File.Exists(mbmPath))
        {
          Texture.filename = mbmPath;
          MBMToTexture(Texture, mipmaps);

        }
        tex.name = newTexture.name;
      }
    }

    internal static void WriteTo(Texture2D cacheTexture, string cacheFile)
    {
      String directory = Path.GetDirectoryName(cacheFile + ".none");
      if (File.Exists(directory))
      {
        File.Delete(directory);
      }
      Directory.CreateDirectory(directory);
      FileStream imgStream = new FileStream(cacheFile, FileMode.Create, FileAccess.Write);
      imgStream.Position = 0;
      byte[] png = cacheTexture.EncodeToPNG();
      imgStream.Write(png, 0, png.Length);
      imgStream.Close();
    }
  }
}
