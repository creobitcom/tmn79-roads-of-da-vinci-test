using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Transport
{
    /// <summary>
    /// Реестр пролётов, которые пешком не пересекаются — их обслуживает транспорт (лифт,
    /// фуникулёр). Единый на все системы, потому что один и тот же факт нужен двоим:
    ///
    /// 1. AITMNPath — чтобы длинносегментный телепорт не перекидывал юнита через пропасть;
    /// 2. подбору исполнителей — чтобы «ближайший» считался с учётом поездки, а не по прямой.
    ///
    /// Без пункта 2 пролёт для дистанции бесплатен, и на карте с лифтом система регулярно
    /// выбирала юнита с ДРУГОЙ стороны: два рабочих менялись местами, катаясь навстречу
    /// друг другу.
    /// </summary>
    public static class TransportSpans
    {
        /// <summary>
        /// Допуск на совпадение точки пути с концом пролёта. В путь A* кладёт позиции нод,
        /// и они не обязаны попадать в зарегистрированную точку ровно.
        /// </summary>
        private const float MatchTolerance = 0.75f;

        private readonly struct Span
        {
            public readonly Vector3 Start;
            public readonly Vector3 End;
            public readonly float CrossingPenalty;

            public Span(Vector3 start, Vector3 end, float crossingPenalty)
            {
                Start = start;
                End = end;
                CrossingPenalty = crossingPenalty;
            }
        }

        private static readonly List<Span> _spans = new();

        public static void Register(Vector3 start, Vector3 end, float crossingPenalty)
        {
            _spans.Add(new Span(start, end, crossingPenalty));
        }

        public static void Unregister(Vector3 start, Vector3 end)
        {
            for (var i = _spans.Count - 1; i >= 0; i--)
            {
                if (_spans[i].Start == start && _spans[i].End == end)
                {
                    _spans.RemoveAt(i);
                }
            }
        }

        /// <summary>Совпадает ли отрезок пути с зарегистрированным пролётом (в любом направлении).</summary>
        public static bool IsSpan(Vector3 point1, Vector3 point2)
        {
            foreach (var span in _spans)
            {
                if ((IsNear(point1, span.Start) && IsNear(point2, span.End))
                    || (IsNear(point1, span.End) && IsNear(point2, span.Start)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Надбавка к расстоянию от from до to за пролёты, которые придётся пересечь.
        ///
        /// Считается пересечением отрезков в плоскости XY: пролёт нарисован ПОПЕРЁК разрыва,
        /// поэтому прямая между точками по разные стороны его обязательно режет, а между
        /// точками на одной стороне — нет. Это дёшево (пролётов на уровне один-два) и не
        /// требует ни поиска пути, ни угадывания «сторон».
        /// </summary>
        public static float GetCrossingPenalty(Vector3 from, Vector3 to)
        {
            if (_spans.Count == 0)
            {
                return 0f;
            }

            var penalty = 0f;

            foreach (var span in _spans)
            {
                if (SegmentsIntersect(from, to, span.Start, span.End))
                {
                    penalty += span.CrossingPenalty;
                }
            }

            return penalty;
        }

        private static bool IsNear(Vector3 first, Vector3 second)
        {
            return (first - second).sqrMagnitude <= MatchTolerance * MatchTolerance;
        }

        private static bool SegmentsIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
        {
            var d1 = Cross(b1, b2, a1);
            var d2 = Cross(b1, b2, a2);
            var d3 = Cross(a1, a2, b1);
            var d4 = Cross(a1, a2, b2);

            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
                   && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(Vector3 origin, Vector3 to, Vector3 point)
        {
            return (to.x - origin.x) * (point.y - origin.y) - (to.y - origin.y) * (point.x - origin.x);
        }
    }
}
