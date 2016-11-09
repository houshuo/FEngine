﻿using MobaGame.FixedMath;

namespace MobaGame.Collision
{
    public class ManifoldResult : DiscreteCollisionDetectorInterface.Result
    {
        protected readonly ObjectPool<ManifoldPoint> pointsPool = new ObjectPool<ManifoldPoint>();

        private PersistentManifold manifoldPtr;

        // we need this for compounds
        private VIntTransform rootTransA = VIntTransform.Identity;
        private VIntTransform rootTransB = VIntTransform.Identity;
        private CollisionObject body0;
        private CollisionObject body1;
        private int partId0;
        private int partId1;
        private int index0;
        private int index1;

        public ManifoldResult()
        {
        }

        public ManifoldResult(CollisionObject body0, CollisionObject body1)
        {
            init(body0, body1);
        }

        public void init(CollisionObject body0, CollisionObject body1)
        {
            this.body0 = body0;
            this.body1 = body1;
            body0.getWorldTransform(out this.rootTransA);
            body1.getWorldTransform(out this.rootTransB);
        }

        public PersistentManifold getPersistentManifold()
        {
            return manifoldPtr;
        }

        public void setPersistentManifold(PersistentManifold manifoldPtr)
        {
            this.manifoldPtr = manifoldPtr;
        }

        public override void setShapeIdentifiers(int partId0, int index0, int partId1, int index1)
        {
            this.partId0 = partId0;
            this.partId1 = partId1;
            this.index0 = index0;
            this.index1 = index1;
        }

        public override void addContactPoint(VInt3 normalOnBInWorld, VInt3 pointInWorld, VFixedPoint depth)
        {
            //order in manifold needs to match

            if (depth > manifoldPtr.getContactBreakingThreshold())
            {
                return;
            }

            bool isSwapped = manifoldPtr.getBody0() != body0;

            VInt3 pointA = normalOnBInWorld * depth + pointInWorld;

            VInt3 localA = new VInt3();
            VInt3 localB = new VInt3();

            if (isSwapped)
            {
                localA = rootTransB.InverseTransformPoint(pointA);
                localB = rootTransA.InverseTransformPoint(pointA);
            }
            else {
                localA = rootTransA.InverseTransformPoint(pointA);
                localB = rootTransB.InverseTransformPoint(pointA);
            }

            ManifoldPoint newPt = pointsPool.Get();
            newPt.init(localA, localB, normalOnBInWorld, depth);

            newPt.positionWorldOnA = pointA;
            newPt.positionWorldOnB = pointInWorld;

            int insertIndex = manifoldPtr.getCacheEntry(newPt);

            newPt.combinedFriction = calculateCombinedFriction(body0, body1);
            newPt.combinedRestitution = calculateCombinedRestitution(body0, body1);

            // BP mod, store contact triangles.
            newPt.partId0 = partId0;
            newPt.partId1 = partId1;
            newPt.index0 = index0;
            newPt.index1 = index1;

            /// todo, check this for any side effects
            if (insertIndex >= 0)
            {
                //const btManifoldPoint& oldPoint = m_manifoldPtr->getContactPoint(insertIndex);
                manifoldPtr.replaceContactPoint(newPt, insertIndex);
            }
            else {
                insertIndex = manifoldPtr.addManifoldPoint(newPt);
            }

            // User can override friction and/or restitution
            if (BulletGlobals.getContactAddedCallback() != null &&
                    // and if either of the two bodies requires custom material
                    ((body0.getCollisionFlags() & CollisionFlags.CUSTOM_MATERIAL_CALLBACK) != 0 ||
                    (body1.getCollisionFlags() & CollisionFlags.CUSTOM_MATERIAL_CALLBACK) != 0))
            {
                //experimental feature info, for per-triangle material etc.
                CollisionObject obj0 = isSwapped ? body1 : body0;
                CollisionObject obj1 = isSwapped ? body0 : body1;
                BulletGlobals.getContactAddedCallback().contactAdded(manifoldPtr.getContactPoint(insertIndex), obj0, partId0, index0, obj1, partId1, index1);
            }

            pointsPool.Release(newPt);
        }

        private static VFixedPoint calculateCombinedFriction(CollisionObject body0, CollisionObject body1)
        {
            VFixedPoint friction = body0.getFriction() * body1.getFriction();

            VFixedPoint MAX_FRICTION = VFixedPoint.Create(10);
            if (friction < -MAX_FRICTION)
            {
                friction = -MAX_FRICTION;
            }
            if (friction > MAX_FRICTION)
            {
                friction = MAX_FRICTION;
            }
            return friction;
        }

        private static VFixedPoint calculateCombinedRestitution(CollisionObject body0, CollisionObject body1)
        {
            return body0.getRestitution() * body1.getRestitution();
        }

        public void refreshContactPoints()
        {
            if (manifoldPtr.getNumContacts() == 0)
            {
                return;
            }

            bool isSwapped = manifoldPtr.getBody0() != body0;

            if (isSwapped)
            {
                manifoldPtr.refreshContactPoints(rootTransB, rootTransA);
            }
            else {
                manifoldPtr.refreshContactPoints(rootTransA, rootTransB);
            }
        }
    }
}
