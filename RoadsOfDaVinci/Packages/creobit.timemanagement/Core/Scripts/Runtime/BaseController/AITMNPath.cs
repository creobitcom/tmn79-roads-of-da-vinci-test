using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Transport;
using Pathfinding;
using Pathfinding.Util;
using UnityEngine;

public class AITMNPath : AIPath
{
	[SerializeField] private float _maxDistance = 1f;

	/// <summary>
	/// Пролёты транспорта лежат в общем реестре TransportSpans: тот же факт нужен подбору
	/// исполнителей, чтобы «ближайший» считался с учётом поездки. Здесь он защищает от
	/// длинносегментного телепорта ниже — такие связи пересекаются транспортом, не пешком.
	/// </summary>
	private static bool IsRegisteredLinkSegment(Vector3 point1, Vector3 point2)
	{
		return TransportSpans.IsSpan(point1, point2);
	}

	protected override void MovementUpdateInternal(float deltaTime, out Vector3 nextPosition,
		out Quaternion nextRotation)
	{
		float currentAcceleration = maxAcceleration;

		// If negative, calculate the acceleration from the max speed
		if (currentAcceleration < 0) currentAcceleration *= -maxSpeed;

		if (updatePosition)
		{
			// Get our current position. We read from transform.position as few times as possible as it is relatively slow
			// (at least compared to a local variable)
			simulatedPosition = tr.position;
		}

		if (updateRotation) simulatedRotation = tr.rotation;

		var currentPosition = simulatedPosition;

		// Update which point we are moving towards
		interpolator.MoveToCircleIntersection2D(currentPosition, pickNextWaypointDist, movementPlane);
		var dir = movementPlane.ToPlane(steeringTarget - currentPosition);

		// Calculate the distance to the end of the path
		float distanceToEnd = dir.magnitude + Mathf.Max(0, interpolator.remainingDistance);

		// Check if we have reached the target
		var prevTargetReached = reachedEndOfPath;
		reachedEndOfPath = distanceToEnd <= endReachedDistance && interpolator.valid;
		if (!prevTargetReached && reachedEndOfPath) OnTargetReached();
		float slowdown;

		// Normalized direction of where the agent is looking
		var forwards = movementPlane.ToPlane(simulatedRotation *
		                                     (orientation == OrientationMode.YAxisForward
			                                     ? Vector3.up
			                                     : Vector3.forward));

		// Check if we have a valid path to follow and some other script has not stopped the character
		bool stopped = isStopped || (reachedDestination && whenCloseToDestination == CloseToDestinationMode.Stop);
		if (interpolator.valid && !stopped)
		{
			// How fast to move depending on the distance to the destination.
			// Move slower as the character gets closer to the destination.
			// This is always a value between 0 and 1.
			slowdown = distanceToEnd < slowdownDistance ? Mathf.Sqrt(distanceToEnd / slowdownDistance) : 1;

			if (reachedEndOfPath && whenCloseToDestination == CloseToDestinationMode.Stop)
			{
				// Slow down as quickly as possible
				velocity2D -= Vector2.ClampMagnitude(velocity2D, currentAcceleration * deltaTime);
			}
			else
			{
				velocity2D += MovementUtilities.CalculateAccelerationToReachPoint(dir, dir.normalized * maxSpeed,
					velocity2D, currentAcceleration, rotationSpeed, maxSpeed, forwards) * deltaTime;
			}
		}
		else
		{
			slowdown = 1;
			// Slow down as quickly as possible
			velocity2D -= Vector2.ClampMagnitude(velocity2D, currentAcceleration * deltaTime);
		}

		velocity2D = MovementUtilities.ClampVelocity(velocity2D, maxSpeed, slowdown,
			slowWhenNotFacingTarget && enableRotation, forwards);

		ApplyGravity(deltaTime);
		if (interpolator != null)
		{
			if (interpolator.Path != null && interpolator.Path.Count > 0)
			{
				if (interpolator.segmentIndex + 1 < interpolator.Path.Count - 1)
				{
					var point1 = interpolator.Path[interpolator.segmentIndex];
					var point2 = interpolator.Path[interpolator.segmentIndex + 1];

					float distance = Vector3.Distance(point1, point2);
					if (distance > _maxDistance && !IsRegisteredLinkSegment(point1, point2))
					{
						Teleport(point2, false);
					}
				}
			}
		}

		// Set how much the agent wants to move during this frame
		var delta2D = lastDeltaPosition =
			CalculateDeltaToMoveThisFrame(movementPlane.ToPlane(currentPosition), distanceToEnd, deltaTime);
		nextPosition = currentPosition + movementPlane.ToWorld(delta2D, verticalVelocity * lastDeltaTime);
		CalculateNextRotation(slowdown, out nextRotation);
	}

}
