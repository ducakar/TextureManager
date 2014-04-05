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
  class Util
  {
    public static readonly string DIR = "TextureManager/";
    public static readonly string PATH = KSPUtil.ApplicationRootPath + "GameData/" + DIR;
    private static readonly char[] CONFIG_DELIMITERS = { ' ', ',' };
    public static bool isDebug = false;

    /**
     * Print a log entry for TextureManager. `String.Format()`-style formatting is supported.
     */
    public static void log(object sender, string s, params object[] args)
    {
      Debug.Log("[TM." + sender.GetType().Name + "] " + String.Format(s, args));
    }

    /**
     * Print a debug entry for TextureManager. `String.Format()`-style formatting is supported.
     */
    public static void debugLog(object sender, string s, params object[] args)
    {
      if (isDebug)
        Debug.Log("[TM." + sender.GetType().Name + "] " + String.Format(s, args));
    }

    /**
     * True iff `i` is a power of two.
     */
    public static bool isPow2(int i)
    {
      return i > 0 && (i & (i - 1)) == 0;
    }

    /**
     * Split a space- and/or comma-separated configuration file value into its tokens.
     */
    public static string[] splitConfigValue(string value)
    {
      return value.Split(CONFIG_DELIMITERS, StringSplitOptions.RemoveEmptyEntries);
    }
  }
}
