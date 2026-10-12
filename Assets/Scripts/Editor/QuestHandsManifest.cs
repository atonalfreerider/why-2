using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Why.EditorTools
{
    /// <summary>
    /// The Quest build is hands-only: after the XR Hands package has declared hand tracking (as optional), this marks it
    /// required and asks for high-frequency tracking, so the headset launches the app without controllers and never
    /// shows its "controllers required" dialog. Runs after every other manifest hook.
    /// </summary>
    public sealed class QuestHandsManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) return;

            XmlDocument doc = new XmlDocument();
            doc.Load(manifest);
            XmlElement root = doc.DocumentElement;
            if (root == null) return;
            XmlElement application = root["application"];

            Ensure(doc, root, "uses-permission", "com.oculus.permission.HAND_TRACKING");
            XmlElement feature = Ensure(doc, root, "uses-feature", "oculus.software.handtracking");
            feature.SetAttribute("required", AndroidNs, "true");
            if (application != null)
            {
                Ensure(doc, application, "meta-data", "com.oculus.handtracking.frequency")
                    .SetAttribute("value", AndroidNs, "HIGH");
                Ensure(doc, application, "meta-data", "com.oculus.handtracking.version")
                    .SetAttribute("value", AndroidNs, "V2.0");
            }

            doc.Save(manifest);
            Debug.Log("[Why] Quest manifest: hand tracking required (hands-only input)");
        }

        /// <summary>The child element with this android:name, created when missing.</summary>
        static XmlElement Ensure(XmlDocument doc, XmlElement parent, string tag, string name)
        {
            foreach (XmlNode n in parent.ChildNodes)
            {
                if (n is XmlElement e && e.Name == tag && e.GetAttribute("name", AndroidNs) == name) return e;
            }

            XmlElement created = doc.CreateElement(tag);
            created.SetAttribute("name", AndroidNs, name);
            parent.AppendChild(created);
            return created;
        }
    }
}
