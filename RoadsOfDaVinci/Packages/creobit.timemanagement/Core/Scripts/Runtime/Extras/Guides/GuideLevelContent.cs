using System;
using System.Collections.Generic;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    [Serializable]
    public class GuideLevelContent
    {
        public List<GuidePageContent> GuideItems = new();

        public bool HasPages => GuideItems is { Count: > 0 };
    }

    [Serializable]
    public class GuidePageContent
    {
        public string PictureName;
        public string Text;
    }
}
