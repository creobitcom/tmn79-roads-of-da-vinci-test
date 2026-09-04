using System.Text;
using Creobit.LA8.EditorTools;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Creobit.LA8.Build
{
    public class CollectiblesCampaignGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var issues = CollectiblesCampaignMapper.GetIssues();

            if (issues.Count == 0)
            {
                return;
            }

            var message = new StringBuilder();
            message.AppendLine(
                $"{issues.Count} collectible(s) do not match the levels that give them, so the edition gate cannot hide them:");

            foreach (var issue in issues)
            {
                message.AppendLine($"  {issue}");
            }

            message.Append("Run 8floor/Collectibles/Bind Collectibles To Levels.");

            throw new BuildFailedException(message.ToString());
        }
    }
}
