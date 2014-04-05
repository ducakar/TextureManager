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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TextureManager
{
  public class TexInfo
  {
    public string name;
    public int width;
    public int height;
    public int resizeWidth;
    public int resizeHeight;
    public string filename;
    public GameDatabase.TextureInfo texture;
    public int scale;
    public int maxSize;
    public bool isNormalMap;
    public bool loadOriginalFirst;
    public bool needsResize;

    public TexInfo(string name)
    {
      this.name = name;
      this.isNormalMap = Compressor.instance.IsNormal(name);
      this.width = 1;
      this.height = 1;
      loadOriginalFirst = false;
      needsResize = false;
    }

    public void SetScalingParams(int scale, int maxSize)
    {
      this.scale = scale;
      this.maxSize = maxSize;
    }

    public void Resize(int width, int height)
    {
      this.width = width;
      this.height = height;
      this.Resize();
    }

    public void Resize()
    {
      resizeWidth = width / scale;
      resizeHeight = height / scale;

      int tmpScale = scale - 1;
      while (resizeWidth < 1 && tmpScale > 0)
      {
        resizeWidth = width / tmpScale--;
      }
      tmpScale = scale - 1;
      while (resizeHeight < 1 && tmpScale > 0)
      {
        resizeHeight = height / tmpScale--;
      }

      if (maxSize != 0)
      {
        if (resizeWidth > maxSize)
        {
          resizeWidth = maxSize;
        }
        if (resizeHeight > maxSize)
        {
          resizeHeight = maxSize;
        }
      }

      needsResize = (resizeHeight != height || resizeWidth != width);
    }
  }

  [KSPAddon(KSPAddon.Startup.EveryScene, false)]
  public class Compressor : MonoBehaviour
  {
    const int GC_COUNT_TRIGGER = 20;
    bool Compressed = false;
    int LastTextureIndex = -1;
    int gcCount = 0;
    long memorySaved = 0;
    ConfigNode config;
    ConfigNode overrides;
    List<String> overridesList = new List<string>();
    List<String> foldersList = new List<string>();
    List<String> readableList = new List<string>();
    List<String> normalList = new List<string>();
    List<String> foldersExList = new List<string>();
    Dictionary<String, long> folderBytesSaved = new Dictionary<string, long>();
    bool config_mipmaps = false;
    bool config_compress = true;
    int config_scale = 1;
    int config_max_size = 1;
    bool config_mipmaps_normals = false;
    bool config_compress_normals = true;
    int config_scale_normals = 1;
    int config_max_size_normals = 1;
    FilterMode config_filter_mode = FilterMode.Bilinear;
    bool config_make_not_readable = false;
    public static Compressor instance = null;

    public Compressor()
    {
      instance = this; // HACK for TexInfo ctor

      PopulateConfig();
      LoadTextures();
    }

    public void Initialize()
    {
      ProcessTextures();
      Compressed = true;

      foreach (GameDatabase.TextureInfo Texture in GameDatabase.Instance.databaseTexture)
      {
        Texture2D texture = Texture.texture;
        Util.log(this, "--------------------------------------------------------");
        Util.log(this, "Name: " + texture.name);
        Util.log(this, "Format: " + texture.format.ToString());
        Util.log(this, "MipMaps: " + texture.mipmapCount.ToString());
        Util.log(this, "Size: " + texture.width.ToString() + "x" + texture.height);
        Util.log(this, "Readable: " + Texture.isReadable);
      }
      long bSaved = memorySaved;
      long kbSaved = (long) (bSaved / 1024f);
      long mbSaved = (long) (kbSaved / 1024f);
      Util.log(this, "Memory Saved : " + bSaved.ToString() + "B");
      Util.log(this, "Memory Saved : " + kbSaved.ToString() + "kB");
      Util.log(this, "Memory Saved : " + mbSaved.ToString() + "MB");

      Converter.instance.DestroyImageBuffer();
    }

    private void LoadTextures()
    {
      UrlDir.UrlConfig[] INTERNALS = GameDatabase.Instance.GetConfigs("TextureManager");
      UrlDir.UrlConfig node = INTERNALS[0];
      {
        List<UrlDir.UrlFile> FilesToRemove = new List<UrlDir.UrlFile>();
        foreach (var file in node.parent.root.AllFiles)
        {
          if (file.fileType == UrlDir.FileType.Texture /*&& foldersList.Exists(n => file.url.StartsWith(n))*/)
          {
            Util.log(this, "L: {0}", file.name);
            TexInfo t = new TexInfo(file.url);
            GameDatabase.TextureInfo Texture = UpdateTexture(t);
            GameDatabase.Instance.databaseTexture.Add(Texture);
            FilesToRemove.Add(file);
            Graphics.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture.texture);
          }
        }
        foreach (var file in FilesToRemove)
        {
          file.parent.files.Remove(file);
        }
        LastTextureIndex = GameDatabase.Instance.databaseTexture.Count - 1;
      }
    }

    private GUISkin _mySkin = null;
    private Rect _mainWindowRect = new Rect(5, 5, 640, 240);
    static Vector2 ScrollFolderList = Vector2.zero;
    int selectedFolder = 0;
    int selectedMode = 0;
    bool guiEnabled = false;
    ConfigNode guiConfig = null;

    private void OnGUI()
    {
      GUI.skin = _mySkin;
      if (HighLogic.LoadedScene == GameScenes.MAINMENU && guiEnabled)
      {
        GUI.backgroundColor = new Color(0, 0, 0, 1);
        String memFormatString = "{0,7}kB {1,4}MB";
        long bSaved = memorySaved;
        long kbSaved = (long) (bSaved / 1024f);
        long mbSaved = (long) (kbSaved / 1024f);
        String totalMemoryString = String.Format("Total Memory Saved: " + memFormatString,
                                                 kbSaved, mbSaved);
        _mainWindowRect = GUI.Window(0x8100, _mainWindowRect, DrawMainWindow, totalMemoryString);
      }
    }

    private void DrawMainWindow(int windowID)
    {
//            GUIStyle gs = new GUIStyle(GUI.skin.label);
//            GUIStyle gsBox = new GUIStyle(GUI.skin.box);

//            int itemFullWidth = (int)_mainWindowRect.width - 30;
      int itemHalfWidth = (int) _mainWindowRect.width / 2 - 20;
//            int itemQuarterWidth = (int)_mainWindowRect.width / 4 - 20;
      int itemMidStart = (int) _mainWindowRect.width - (15 + itemHalfWidth);
      int itemThirdWidth = (int) _mainWindowRect.width / 3 - 20;
      int itemTwoThirdStart = itemThirdWidth + 20;
      int itemTwoThirdWidth = (int) _mainWindowRect.width - (35 + itemThirdWidth);
//            int itemQuarterThirdWidth = itemHalfWidth + 5 - itemTwoThirdStart;

      GUI.Box(new Rect(0, 0, _mainWindowRect.width, _mainWindowRect.height), "");

      GUI.Box(new Rect(10, 20, itemThirdWidth, 210), "");
      String[] folderList = foldersExList.ToArray();
      ScrollFolderList =
        GUI.BeginScrollView(new Rect(15, 25, itemThirdWidth - 10, 195), ScrollFolderList,
                            new Rect(0, 0, itemThirdWidth - 30, 25 * folderList.Length));

      float folderWidth = folderList.Length > 7 ? itemThirdWidth - 30 : itemThirdWidth - 10;
      selectedFolder = selectedFolder >= folderList.Length ? 0 : selectedFolder;
      int OldSelectedFolder = selectedFolder;
      selectedFolder = GUI.SelectionGrid(new Rect(0, 0, folderWidth, 25 * folderList.Length),
                                         selectedFolder, folderList, 1);
      GUI.EndScrollView();

      String folder = folderList[selectedFolder];
      if (folder != "UNMANAGED" && (OldSelectedFolder != selectedFolder || guiConfig == null))
      {
        String config = System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData",
                                                     "*" + folder + ".tcfg",
                                                     System.IO.SearchOption.AllDirectories)[0];
        guiConfig = ConfigNode.Load(config);
      }

      String memFormatString = "{0,7}kB {1,4}MB";
      long bSaved = folderBytesSaved[folderList[selectedFolder]];
      long kbSaved = (long) (bSaved / 1024f);
      long mbSaved = (long) (kbSaved / 1024f);
      String memoryString = String.Format("Memory Saved: " + memFormatString, kbSaved, mbSaved);
      GUI.Label(new Rect(itemMidStart, 55, itemHalfWidth, 25), memoryString);

      String[] Modes = { "Normal List", "Overrides" };
      //selectedMode = GUI.SelectionGrid(new Rect(itemTwoThirdStart, 25, itemQuarterThirdWidth, 25 * Modes.Length), selectedMode, Modes, 1);
      selectedMode = GUI.Toolbar(new Rect(itemMidStart, 25, itemHalfWidth, 25),
                                 selectedMode, Modes);
      if (selectedMode == 0)
      {
        GUI.Box(new Rect(itemTwoThirdStart, 85, itemTwoThirdWidth, 145), "");
      }
      GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    public void ProcessTextures()
    {
      if (!Compressed)
      {
        Util.log(this, "GameDatabase.Instance.databaseTexture.Count: {0}",
                 GameDatabase.Instance.databaseTexture.Count);
      }
      if (!Compressed && GameDatabase.Instance.databaseTexture.Count > 0)
      {
        int LocalLastTextureIndex = GameDatabase.Instance.databaseTexture.Count - 1;
        if (LastTextureIndex != LocalLastTextureIndex)
        {
          for (int i = LastTextureIndex + 1; i < GameDatabase.Instance.databaseTexture.Count; i++)
          {
            GameDatabase.TextureInfo Texture = GameDatabase.Instance.databaseTexture[i];
            LastTextureIndex = i;

            int originalWidth = Texture.texture.width;
            int originalHeight = Texture.texture.height;
            TextureFormat originalFormat = Texture.texture.format;
            bool originalMipmaps = Texture.texture.mipmapCount == 1 ? false : true;
            String folder = "UNMANAGED";
            if (config_compress)
            {
              tryCompress(Texture);
            }
            if (!foldersExList.Contains(folder))
            {
              foldersExList.Add(folder);
            }
            updateMemoryCount(originalWidth, originalHeight, originalFormat, originalMipmaps,
                              Texture, folder);
            gcCount++;
          }
          if (gcCount > GC_COUNT_TRIGGER)
          {
            System.GC.Collect();
            gcCount = 0;
          }
        }
      }
      else if (HighLogic.LoadedScene == GameScenes.MAINMENU)
      {
        bool alt = (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        if (alt && Input.GetKeyDown(KeyCode.M))
        {
          guiEnabled = !guiEnabled;
        }
      }
    }

    public bool IsNormal(String name)
    {
      bool isNormal = name.EndsWith("NRM") || normalList.Contains(name);
      String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + name + ".mbm";
      if (!isNormal && File.Exists(originalTextureFile))
      {
        FileStream stream = File.OpenRead(originalTextureFile);
        //while stream is open, if it is an MBM, flag normal maps.
        stream.Position = 12;
        if (stream.ReadByte() == 1)
        {
          isNormal = true;
        }
        stream.Close();
      }
      return isNormal;
    }

    private void SetNormalMap(GameDatabase.TextureInfo Texture)
    {
      Texture.isNormalMap = IsNormal(Texture.name);
    }

    private void tryCompress(GameDatabase.TextureInfo Texture)
    {
      Texture2D tex = Texture.texture;
      if (tex.format != TextureFormat.DXT1 && tex.format != TextureFormat.DXT5)
      {
        try
        {
          tex.GetPixel(0, 0);
          tex.Compress(true);
          Texture.isCompressed = true;
          Texture.isReadable = true;
        }
        catch
        {
          Texture.isReadable = false;
        }
      }
    }

    private void PopulateConfig()
    {
      config = GameDatabase.Instance.GetConfigNode(Util.DIR + "@Default/TextureManager");
      if (config == null)
      {
        return;
      }

      String dbg = config.GetValue("DBG");
      if (dbg != null)
      {
        Util.isDebug = true;
      }

      List<String> configfiles = new List<string>();

      if (System.IO.Directory.Exists(Util.PATH + "Configs"))
      {
        configfiles.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData",
                                                          "*.tcfg",
                                                          System.IO.SearchOption.AllDirectories));
      }

      overrides = config.GetNode("OVERRIDES");
      ConfigNode folders = config.GetNode("FOLDERS");
      ConfigNode normals = config.GetNode("NORMAL_LIST");

      if (overrides == null)
      {
        overrides = new ConfigNode("OVERRIDES");
      }
      if (folders == null)
      {
        folders = new ConfigNode("FOLDERS");
      }
      if (normals == null)
      {
        normals = new ConfigNode("NORMAL_LIST");
      }
      String pathStart = (Util.PATH + "Configs/").Replace('\\', '/');
      foreach (String configFile in configfiles)
      {
        String unixConfigFile = configFile.Replace('\\', '/');
        String folder = unixConfigFile.Replace(pathStart, "").Replace(".tcfg", "");
        ConfigNode configFolder = ConfigNode.Load(unixConfigFile);
        String enabledString = configFolder.GetValue("config_enabled");
        bool isEnabled = false;
        if (enabledString != null)
        {
          bool.TryParse(enabledString, out isEnabled);
        }
        if (isEnabled)
        {
          folders.AddValue("folder", folder);
          ConfigNode modOverrides = configFolder.GetNode("OVERRIDES");
          ConfigNode modNormals = configFolder.GetNode("NORMAL_LIST");
          CopyConfigNode(modOverrides, overrides);
          CopyConfigNode(modNormals, normals);
        }
      }

      foreach (ConfigNode node in overrides.nodes)
      {
        overridesList.Add(node.name);
      }
      foreach (ConfigNode.Value folder in folders.values)
      {
        foldersList.Add(folder.value);
      }
      foreach (ConfigNode.Value texture in normals.values)
      {
        normalList.Add(texture.value);
      }

      String mipmapsString = config.GetValue("mipmaps");
      String compressString = config.GetValue("compress");
      String scaleString = config.GetValue("scale");
      String max_sizeString = config.GetValue("max_size");
      String filter_modeString = config.GetValue("filter_mode");
      String make_not_readableString = config.GetValue("make_not_readable");

      bool.TryParse(mipmapsString, out config_mipmaps);
      bool.TryParse(compressString, out config_compress);
      int.TryParse(scaleString, out config_scale);
      int.TryParse(max_sizeString, out config_max_size);
      config_filter_mode = (FilterMode) Enum.Parse(typeof(FilterMode), filter_modeString);
      bool.TryParse(make_not_readableString, out config_make_not_readable);

      String mipmapsString_normals = config.GetValue("mipmaps_normals");
      String compressString_normals = config.GetValue("compress_normals");
      String scaleString_normals = config.GetValue("scale_normals");
      String max_sizeString_normals = config.GetValue("max_size_normals");

      bool.TryParse(mipmapsString_normals, out config_mipmaps_normals);
      bool.TryParse(compressString_normals, out config_compress_normals);
      int.TryParse(scaleString_normals, out config_scale_normals);
      int.TryParse(max_sizeString_normals, out config_max_size_normals);

      Util.log(this, "Settings:");
      Util.log(this, "   mipmaps: " + config_mipmaps);
      Util.log(this, "   compress: " + config_compress);
      Util.log(this, "   scale: " + config_scale);
      Util.log(this, "   max_size: " + config_max_size);
      Util.log(this, "   mipmaps_normals: " + config_mipmaps_normals);
      Util.log(this, "   compress_normals: " + config_compress_normals);
      Util.log(this, "   scale_normals: " + config_scale_normals);
      Util.log(this, "   max_size_normals: " + config_max_size_normals);
      Util.log(this, "   filter_mode: " + config_filter_mode);
      Util.log(this, "   make_not_readable: " + config_make_not_readable);
      Util.log(this, "   normal List: ");
      foreach (String normal in normalList)
      {
        Util.debugLog(this, "      " + normal);
      }
    }

    private void CopyConfigNode(ConfigNode original, ConfigNode copy)
    {
      if (original != null)
      {
        foreach (ConfigNode node in original.nodes)
        {
          copy.AddNode(node);
        }
        foreach (ConfigNode.Value value in original.values)
        {
          copy.AddValue(value.name, value.value);
        }
      }
    }

    public GameDatabase.TextureInfo UpdateTexture(TexInfo texture)
    {
      string overrideName = overridesList.Find(
                              n => texture.name.Length == Regex.Match(texture.name, n).Length);

      bool mipmaps = config_mipmaps;
      bool compress = config_compress;
      int scale = config_scale;
      int maxSize = config_max_size;
      if (texture.isNormalMap)
      {
        mipmaps = config_mipmaps_normals;
        compress = config_compress_normals;
        scale = config_scale_normals;
        maxSize = config_max_size_normals;
      }
      FilterMode filterMode = config_filter_mode;
      bool makeNotReadable = config_make_not_readable;

      if (overrideName != null)
      {
        ConfigNode overrideNode = overrides.GetNode(overrideName);
        String normalString = texture.isNormalMap ? "_normals" : "";
        String mipmapsString = overrideNode.GetValue("mipmaps" + normalString);
        String compressString = overrideNode.GetValue("compress" + normalString);
        String scaleString = overrideNode.GetValue("scale" + normalString);
        String max_sizeString = overrideNode.GetValue("max_size" + normalString);
        String filter_modeString = overrideNode.GetValue("filter_mode");
        String make_not_readableString = overrideNode.GetValue("make_not_readable");
        if (mipmapsString != null)
        {
          bool.TryParse(mipmapsString, out mipmaps);
        }
        if (compressString != null)
        {
          bool.TryParse(compressString, out compress);
        }
        if (scaleString != null)
        {
          int.TryParse(scaleString, out scale);
        }
        if (filter_modeString != null)
        {
          try
          {
            filterMode = (FilterMode) Enum.Parse(typeof(FilterMode), filter_modeString);
          }
          catch
          {
            filterMode = config_filter_mode;
          }
        }
        if (make_not_readableString != null)
        {
          bool.TryParse(make_not_readableString, out makeNotReadable);
        }
        if (max_sizeString != null)
        {
          int.TryParse(max_sizeString, out maxSize);
        }
      }

      texture.SetScalingParams(scale, maxSize);

      GameDatabase.TextureInfo ret = CacheController.instance.FetchCacheTexture(
                                       texture, compress, mipmaps,
                                       makeNotReadable && !readableList.Contains(texture.name));
      ret.texture.filterMode = filterMode;
      return ret;
    }

    private void updateMemoryCount(int originalWidth, int originalHeight,
                                   TextureFormat originalFormat, bool originalMipmaps,
                                   GameDatabase.TextureInfo Texture, String folder)
    {
      int saved = CacheController.instance.MemorySaved(originalWidth, originalHeight,
                                                       originalFormat, originalMipmaps, Texture);
      memorySaved += saved;

      if (!folderBytesSaved.ContainsKey(folder))
      {
        folderBytesSaved.Add(folder, 0);
      }
      long folderSaved = folderBytesSaved[folder] + saved;
      folderBytesSaved[folder] = folderSaved;

      Util.log(this, "Saved " + saved + "B");
      Util.log(this, "Accumulated Saved " + memorySaved + "B");
    }
  }
}
