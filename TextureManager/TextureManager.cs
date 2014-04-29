/*
 * Copyright © 2014 Davorin Učakar
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
using UnityEngine;

namespace TextureManager
{
  [KSPAddon(KSPAddon.Startup.Instantly, true)]
  public class TextureReplacer : MonoBehaviour
  {
    // Generic texture replacement parameters.
    private GameScenes lastScene = GameScenes.LOADING;
    private bool isInitialised = false;

    public void Start()
    {
      try
      {
        DontDestroyOnLoad(this);

        Converter.instance = new Converter();
        CacheController.instance = new CacheController();
        Compressor.instance = new Compressor();
        Replacer.instance = new Replacer();
//        Asteroids.instance = new Asteroids();
        Reflections.instance = new Reflections();
        Personaliser.instance = new Personaliser();

        foreach (UrlDir.UrlConfig file in GameDatabase.Instance.GetConfigs("TextureReplacer"))
        {
          Util.log("Reading configuration: {0}", file.url);

          Replacer.instance.readConfig(file.config);
//          Asteroids.instance.readConfig(file.config);
          Reflections.instance.readConfig(file.config);
          Personaliser.instance.readConfig(file.config);
        }

        Resources.UnloadUnusedAssets();
        System.GC.Collect();
      }
      catch (Exception e)
      {
        Util.log("{0}: {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
      }
    }

    public void LateUpdate()
    {
      try
      {
        if (!isInitialised)
        {
          // Compress textures, generate mipmaps, convert DXT5 -> DXT1 if necessary etc.
          Compressor.instance.ProcessTextures();

          if (GameDatabase.Instance.IsReady())
          {
            Compressor.instance.Initialize();
            Replacer.instance.initialise();
//            Asteroids.instance.initialise();
            Reflections.instance.initialise();
            Personaliser.instance.initialise();

            isInitialised = true;
          }
        }
        else
        {
          // Schedule general texture replacement pass at the beginning of each scene. Textures are
          // still loaded several frames after scene switch so this pass must be repeated multiple
          // times. Especially problematic is the main menu that resets skybox texture twice, second
          // time being several tens of frames after the load (depending on frame rate).
          if (HighLogic.LoadedScene != lastScene)
          {
            lastScene = HighLogic.LoadedScene;

            Replacer.instance.resetScene();
//            Asteroids.instance.resetScene();
            Personaliser.instance.resetScene();
          }

          Replacer.instance.updateScene();
//          Asteroids.instance.updateScene();
          Personaliser.instance.updateScene();
        }
      }
      catch (Exception e)
      {
        Util.log("{0}: {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
      }
    }

    public void OnDestroy()
    {
      Reflections.instance.destroy();
    }
  }
}
