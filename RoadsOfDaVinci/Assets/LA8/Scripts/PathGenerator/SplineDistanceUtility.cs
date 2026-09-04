using UnityEngine;
using UnityEngine.Splines;

public static class SplineDistanceUtility
{
    public static float GetTAtDistance(
        Spline spline,
        float distance,
        int resolution = 100)
    {
        if (spline == null || distance <= 0f)
            return 0f;

        float totalLength = spline.GetLength();
        distance = Mathf.Clamp(distance, 0f, totalLength);

        float accumulated = 0f;
        Vector3 prev = spline.EvaluatePosition(0f);

        for (int i = 1; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            Vector3 curr = spline.EvaluatePosition(t);

            float segment = Vector3.Distance(prev, curr);

            if (accumulated + segment >= distance)
            {
                float remain = distance - accumulated;
                float lerp = segment > 0f ? remain / segment : 0f;
                return Mathf.Lerp((i - 1) / (float)resolution, t, lerp);
            }

            accumulated += segment;
            prev = curr;
        }

        return 1f;
    }
}
