using System;
using System.Collections.Generic;
using Unity.Properties.UI;
using Unity.Build.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Build.iOS
{
    sealed class iOSIconsInspector : Inspector<iOSIcons>
    {
        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(new IconSelector("iPhone 2x", 120, 120, Target.iPhone2x, val => Target = new iOSIcons(Target) { iPhone2x = val } ));
            root.Add(new IconSelector("iPhone 3x", 180, 180, Target.iPhone3x, val => Target = new iOSIcons(Target) { iPhone3x = val } ));
            root.Add(new IconSelector("iPad, iPad mini 2x", 152, 152, Target.iPad2x, val => Target = new iOSIcons(Target) { iPad2x = val } ));
            root.Add(new IconSelector("iPad Pro 2x", 167, 167, Target.iPadPro2x, val => Target = new iOSIcons(Target) { iPadPro2x = val } ));
            root.Add(new IconSelector("App Store", 1024, 1024, Target.AppStore, val => Target = new iOSIcons(Target) { AppStore = val } ));
            return root;
        }
    }
}
