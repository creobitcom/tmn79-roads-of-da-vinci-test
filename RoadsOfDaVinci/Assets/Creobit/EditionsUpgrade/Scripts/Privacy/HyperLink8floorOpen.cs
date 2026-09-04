using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Creobit.EditionsUpgrade
{
    public class HyperLink8floorOpen : MonoBehaviour, IPointerClickHandler
    {
        private const string PrivacyLink = "https://8floor.net/pp";
        private const string TosLink = "https://8floor.net/tos";
		
        [SerializeField, Tooltip("The UI GameObject having the TextMesh Pro component.")]
        private TMP_Text text = default;

        public void OnPointerClick(PointerEventData eventData)
        {
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, null);
            if (linkIndex < 0)
            {
                return;
            }

            var linkId = text.textInfo.linkInfo[linkIndex].GetLinkID();

            string url = string.Empty;

            if (string.Equals(linkId, "url1"))
            {
                url = TosLink;
            }
            else if (string.Equals(linkId, "url2"))
            {
                url = PrivacyLink;
            }

            var length = text.textInfo.linkInfo[linkIndex].linkTextLength;
            var firstIndex = text.textInfo.linkInfo[linkIndex].linkTextfirstCharacterIndex;
            for (int i = 0; i < length; ++i)
            {
                var charIndex = firstIndex + i;
                var meshIndex = text.textInfo.characterInfo[charIndex].materialReferenceIndex;

                var vertexIndex = text.textInfo.characterInfo[charIndex].vertexIndex;
                if (vertexIndex == 0)
                {
                    continue;
                }

                Color32[] vertexColors = text.textInfo.meshInfo[meshIndex].colors32;
                vertexColors[vertexIndex + 0] = Color.grey;
                vertexColors[vertexIndex + 1] = Color.grey;
                vertexColors[vertexIndex + 2] = Color.grey;
                vertexColors[vertexIndex + 3] = Color.grey;

            }
            
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            Application.OpenURL(url);
        }
    }
}