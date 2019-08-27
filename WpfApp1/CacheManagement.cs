﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SquintScript
{
    partial class Ctr
    {
        public static class DataCache
        {
            public static class SquintDb
            {
                private static SquintdBModel _Context = new SquintdBModel();
                public static SquintdBModel Context
                {
                    get { return _Context; }
                }
                public static void Reload()
                {
                    _Context = new SquintdBModel();
                }
                public static List<string> GetProtocolNameByProperty(ProtocolTypes ProtocolType = ProtocolTypes.All, string ProtocolAuthor = null, TreatmentCentres TreatmentCentre = TreatmentCentres.All)
                {
                    List<string> ProtocolNames;
                    using (var DbContext = SquintDb.Context)
                    {
                        IQueryable<DbProtocol> ProtocolFilter = DbContext.DbLibraryProtocols;
                        if (ProtocolType != ProtocolTypes.All)
                            ProtocolFilter = ProtocolFilter.Where(x => x.DbProtocolType.ProtocolType == (int)ProtocolType);
                        if (ProtocolAuthor != null)
                            ProtocolFilter = ProtocolFilter.Where(x => x.DbUser_ProtocolAuthor.ARIA_ID == ProtocolAuthor);
                        if (TreatmentCentre != TreatmentCentres.All)
                            ProtocolFilter = ProtocolFilter.Where(x => x.DbTreatmentCentre.TreatmentCentre == (int)TreatmentCentre);
                        return ProtocolNames = ProtocolFilter.Select(x => x.ProtocolName).ToList();
                    }
                }
            }

            //Events
            public static event EventHandler<int> ConstraintDeleted;
            public static event EventHandler<int> ComponentDeleted;
            public static event EventHandler<int> PlanDeleted;
            public static event EventHandler<int> ECSIDDeleted;

            public static Session CurrentSession { get; private set; }
            public static Protocol CurrentProtocol { get; private set; }
            public static AsyncPatient Patient { get; private set; }
            private static Dictionary<int, Constraint> _Constraints = new Dictionary<int, Constraint>(); // lookup constraint by its key
            private static Dictionary<int, ECPlan> _Plans = new Dictionary<int, ECPlan>();
            private static Dictionary<int, Component> _Components = new Dictionary<int, Component>(); // lookup component by key
            private static Dictionary<int, Assessment> _Assessments = new Dictionary<int, Assessment>();
            //private static Dictionary<int, Constituent> _Constituents = new Dictionary<int, Constituent>();
            private static Dictionary<int, ConstraintThreshold> _ConstraintThresholds = new Dictionary<int, ConstraintThreshold>();
            private static Dictionary<int, AsyncPlan> _AsyncPlans = new Dictionary<int, AsyncPlan>();
            private static Dictionary<string, AsyncCourse> _Courses = new Dictionary<string, AsyncCourse>();
            private static Dictionary<int, object> _EclipsePlans = new Dictionary<int, object>(); // may be PlanSetup or PlanSum
            private static Dictionary<string, int> _CourseIDbyName = new Dictionary<string, int>();
            private static Dictionary<int, ECSID> _ECSIDs = new Dictionary<int, ECSID>();
            private static Dictionary<string, bool> _isCourseLoaded = new Dictionary<string, bool>(); // check to see whether course has been loaded by EclipseID/name
            public static Dictionary<int, int> CourseLookupFromPlan = new Dictionary<int, int>(); // lookup course PK by planPK
            public static Dictionary<int, int> CourseLookupFromSum = new Dictionary<int, int>(); // lookup course PK by plansumPK
            private static Dictionary<int, StructureLabel> _StructureLabels = new Dictionary<int, StructureLabel>();
            //private static List<AssignedId> AssignedIds = new List<AssignedId>();
            private static bool areStructuresLoaded = false;
            private static readonly object CourseLoadLock = new object();


            public static void RegisterUser(string UserId)
            {
                using (var Context = new SquintdBModel())
                {
                    var User = Context.DbUsers.FirstOrDefault(x => x.ARIA_ID == UserId);
                    if (User == null)
                    {
                        // Add new user;
                        lock (LockDb)
                        {
                            User = Context.DbUsers.Create();
                            Context.DbUsers.Add(User);
                            User.ID = IDGenerator();
                            User.PermissionGroupID = 1;
                            User.FirstName = A.CurrentUserName();
                            User.ARIA_ID = A.CurrentUserId();
                            Context.SaveChanges();
                        }
                    }
                }
            }
            public static void OpenPatient(string PID)
            {
                Patient = A.OpenPatient(PID);
            }
            public static void ClosePatient()
            {
                ClearAssessments(); // method notifies that DataCache.Assessments have been cleared
                ClearPlans();
                ClearCourses();
                if (A != null)
                    A.ClosePatient();
            }
            public static void RefreshPatient()
            {
                if (PatientLoaded)
                {
                    ClearCourses();
                    ClearAsyncPlans();
                    if (A != null)
                        A.ClosePatient();
                    A.OpenPatient(PatientID);
                }
            }
            public static void CreateSession()
            {
                CurrentSession = new Session();
            }
            public static void SetSession(Session S)
            {
                CurrentSession = S;
            }
            public static void SetProtocol(Protocol P)
            {
                CurrentProtocol = P;
            }
            public static void AddPlan(ECPlan P)
            {
                _Plans.Add(P.ID, P);
                P.PlanDeleting += OnPlanDeleting;
            }
            public static void OnPlanDeleting(object sender, int ID)
            {
                _Plans[ID].PlanDeleting -= OnPlanDeleting;
                _Plans.Remove(ID);
            }
            public static void DeletePlan(int ID)
            {
                _Plans.Remove(ID);
            }
            public static ECPlan GetPlan(int ID)
            {
                return _Plans[ID];
            }
            public static IEnumerable<ECPlan> GetAllPlans()
            {
                return _Plans.Values;
            }
            public static void ClearPlans()
            {
                _Plans.Clear();
            }

            public static void AddConstraint(Constraint C)
            {
                _Constraints.Add(C.ID, C);
                C.ConstraintDeleted += OnConstraintDeleted;
            }
            public static void OnConstraintDeleted(object sender, int ID)
            {
                _Constraints[ID].ConstraintDeleted -= OnConstraintDeleted;
                _Constraints.Remove(ID);
                foreach (ConstraintThreshold CT in _ConstraintThresholds.Values.Where(x => x.ConstraintID == ID).ToList())
                    _ConstraintThresholds.Remove(CT.ID);
            }
            public static Constraint GetConstraint(int ID)
            {
                if (_Constraints.ContainsKey(ID))
                    return _Constraints[ID];
                else
                {
                    using (var Context = new SquintdBModel())
                    {
                        var C = new Constraint(Context.DbConstraints.Find(ID));
                        AddConstraint(C);
                        return C;
                    }
                }
            }
            public static IEnumerable<Constraint> GetConstraintsInComponent(int CompID)
            {
                return _Constraints.Values.Where(x => x.ComponentID == CompID);
            }
            public static IEnumerable<Constraint> GetAllConstraints()
            {
                return _Constraints.Values;
            }

            public static void AddComponent(Component C)
            {
                _Components.Add(C.ID, C);
                C.ComponentDeleted += OnComponentDeleted;
            }
            public static Component DuplicateComponent(int CompID)
            {
                Component Cnew = new Component(_Components[CompID]);
                _Components.Add(Cnew.ID, Cnew);
                return Cnew;
            }

            public static void OnComponentDeleted(object sender, int ID)
            {
                _Components[ID].ComponentDeleted -= OnComponentDeleted;
                _Components.Remove(ID);
            }
            public static void DeleteComponent(int ID)
            {
                _Components.Remove(ID);
            }
            public static Component GetComponent(int ID)
            {
                return _Components[ID];
            }
            public static IEnumerable<Component> GetAllComponents()
            {
                return _Components.Values;
            }

            public static void AddConstraintThreshold(ConstraintThreshold C)
            {
                _ConstraintThresholds.Add(C.ID, C);
            }
            public static void AddConstraintThreshold(ConstraintThresholdNames Name, ConstraintThresholdTypes Goal, Constraint Con, double value)
            {
                ConstraintThreshold CT = new ConstraintThreshold(Name, Goal, Con, value);
                _ConstraintThresholds.Add(CT.ID, CT);
            }

            public static ConstraintThreshold LoadConstraintThreshold(DbConThreshold DbCT, int ConId)
            {
                ConstraintThreshold CT = new ConstraintThreshold(DbCT, _Constraints[ConId]);
                AddConstraintThreshold(CT);
                return CT;
            }
            public static void DeleteConstraintThreshold(ConstraintThreshold C)
            {
                _ConstraintThresholds.Remove(C.ID);
            }
            public static void DeleteConstraintThreshold(int ID)
            {
                _ConstraintThresholds.Remove(ID);
            }
            public static ConstraintThreshold GetConstraintThreshold(int ID)
            {
                return _ConstraintThresholds[ID];
            }
            public static IEnumerable<ConstraintThreshold> GetConstraintThresholdByConstraintId(int ConID)
            {
                return _ConstraintThresholds.Values.Where(x => x.ConstraintID == ConID);
            }
            public static IEnumerable<ConstraintThreshold> GetAllConstraintThresholds()
            {
                return _ConstraintThresholds.Values;
            }

            //public static void AddConstituent(Constituent C)
            //{
            //    _Constituents.Add(C.ID, C);
            //    C.ConstituentDeleting += OnConstituentDeleting;
            //}
            //public static Constituent DuplicateConstituent(int DupId)
            //{
            //    Constituent SCC = _Constituents[DupId];
            //    Component DupConstituentComponent = DuplicateComponent(SCC.ConstituentCompID); // duplicate the constituent component
            //    AddComponent(DupConstituentComponent);
            //    Constituent DupSCC = new Constituent(SCC.ComponentID, DupConstituentComponent.ID);
            //    AddConstituent(DupSCC);
            //    return DupSCC;
            //}
            //public static void DeleteConstraintThreshold(Constituent C)
            //{
            //    _Constituents.Remove(C.ID);
            //}
            //public static void OnConstituentDeleting(object sender, int ID)
            //{
            //    _Constituents[ID].ConstituentDeleting -= OnConstituentDeleting;
            //    _Constituents.Remove(ID);
            //}
            //public static Constituent GetConstituent(int ID)
            //{
            //    return _Constituents[ID];
            //}
            //public static IEnumerable<Constituent> GetConstituentsByComponentID(int CompID)
            //{
            //    return _Constituents.Values.Where(x => x.ComponentID == CompID);
            //}
            //public static IEnumerable<Constituent> GetAllConstituents()

            public static void AddECSID(ECSID E)
            {
                _ECSIDs.Add(E.ID, E);
                E.ECSIDDeleting += OnECSIDDeleting;
            }
            public static void OnECSIDDeleting(object sender, int ID)
            {
                _ECSIDs[ID].ECSIDDeleting -= OnECSIDDeleting;
                _ECSIDs.Remove(ID);

            }
            public static ECSID GetECSID(int ID)
            {
                if (!_ECSIDs.ContainsKey(ID))
                {
                    DbECSID DbE = SquintDb.Context.DbECSIDs.Find(ID);
                    if (DbE == null)
                    {
                        throw new Exception("Can't find key in GetECSID");
                    }
                    else
                    {
                        _ECSIDs.Add(DbE.ID, new ECSID(DbE));
                    }
                }
                return _ECSIDs[ID];
            }
            public static IEnumerable<ECSID> GetAllECSIDs()
            {
                return _ECSIDs.Values;
            }

            //public static void AddAssignedId(ECSID ECS, AsyncPlan LinkedPlan, string StructureId)
            //{
            //    AssignedIds.Add(new AssignedId(ECS, LinkedPlan, StructureId));
            //}
            //public static AssignedId GetAssignedId(int ECSID_Id_in)
            //{
            //    return AssignedIds.Find(x => x.SessionId == CurrentSession.ID && x.ECSID_Id == ECSID_Id_in);
            //}
            private static void LoadStructures()
            {
                _StructureLabels.Clear();
                foreach (DbStructureLabel DbSL in SquintDb.Context.DbStructureLabels)
                    _StructureLabels.Add(DbSL.ID, new StructureLabel(DbSL));
            }
            public static StructureLabel GetStructureLabel(int Id)
            {
                return _StructureLabels[Id];
            }
            public static string GetStructureCode(int Id)
            {
                return _StructureLabels[Id].Code;
            }
            public static IEnumerable<StructureLabel> GetAllStructureLabels()
            {
                return _StructureLabels.Values;
            }
            public static string GetLabelByCode(string Code)
            {
                var Label = _StructureLabels.Values.FirstOrDefault(x => x.Code == Code);
                if (Label != null)
                    return Label.LabelName;
                else
                    return "Label not found";
            }
            public static void AddAssessment(Assessment A)
            {
                _Assessments.Add(A.ID, A);
            }
            public static void DeleteAssessment(int ID)
            {
                _Assessments.Remove(ID);
            }
            public static void ClearAssessments()
            {
                _Assessments.Clear();
                _AssessmentNameIterator = 1;
            }
            public static Assessment GetAssessment(int ID)
            {
                if (_Assessments.ContainsKey(ID))
                    return _Assessments[ID];
                else
                    throw new Exception("Attempt to get assessment that doesn't exist in cache (GetAssessment)");
            }
            public static IEnumerable<Assessment> GetAllAssessments()
            {
                return _Assessments.Values;
            }
            //public static List<AssessmentPreviewView> GetAssessmentPreviews(string PID)
            //{
            //    List<AssessmentPreviewView> values = new List<AssessmentPreviewView>();
            //    foreach (DbAssessment DbA in SquintDb.Context.DbAssessments.Where(x => x.PID == PID))
            //    {
            //        values.Add(new AssessmentPreviewView(DbA.ID, DbA.AssessmentName, DbA.DbProtocol.ProtocolName, DbA.SquintUser, DbA.DateOfAssessment, DbA.Comments));
            //    }
            //    return values;
            //}

            public static void AddAsyncPlan(AsyncPlan P)
            {
                _AsyncPlans.Add(P.HashId, P);
            }
            public static bool isPlanLoaded(string CourseName, string PlanName)
            {
                AsyncPlan p = _AsyncPlans.Values.Where(x => x.Course.Id == CourseName && x.Id == PlanName).SingleOrDefault();
                if (p == null)
                    return false;
                else
                    return true;
            }
            public static AsyncPlan GetAsyncPlan(int ID)
            {
                return _AsyncPlans[ID];
            }
            public static AsyncPlan GetAsyncPlan(string PlanName, string CourseName, ComponentTypes PlanType)
            {
                return _AsyncPlans.Values.FirstOrDefault(x => x.Id == PlanName && x.Course.Id == CourseName && x.PlanType == PlanType);
            }
            public static IEnumerable<AsyncPlan> GetAsyncPlans()
            {
                return _AsyncPlans.Values;
            }
            public static async Task<List<AsyncPlan>> GetPlansByCourseName(string CourseName, IProgress<int> progress = null)
            {
                if (!_isCourseLoaded.ContainsKey(CourseName))
                {
                    AsyncCourse C = await GetCourseAsync(CourseName, progress);
                    if (!_isCourseLoaded.ContainsKey(CourseName)) // need to test this in case concurrent threads are awaiting GetCourseAync for the same course.
                        _isCourseLoaded.Add(CourseName, true);
                }
                return _AsyncPlans.Values.Where(x => x.Course.Id == CourseName).ToList();
            }
            public async static Task<List<string>> GetPlanIdsByCourseName(string CourseName, IProgress<int> progress = null)
            {
                List<AsyncPlan> P = await DataCache.GetPlansByCourseName(CourseName, progress);
                return P.Select(x => x.Id).ToList();
            }
            public static void ClearAsyncPlans()
            {
                _AsyncPlans.Clear();
            }

            public static void DeleteCourse(string Id)
            {
                _Courses.Remove(Id);
            }
            public static async Task<AsyncCourse> GetCourse(string Id)
            {
                if (_Courses.ContainsKey(Id))
                    return _Courses[Id];
                else
                {
                    var C = await Patient.GetCourse(Id, null);
                    if (C == null)
                    {
                        return null; // no such course
                    }
                    _Courses.Add(C.Id, C);
                    foreach (AsyncPlan P in C.Plans)
                    {
                        AddAsyncPlan(P);
                    }
                    return C;
                }
            }
            public async static Task<AsyncCourse> GetCourseAsync(string CourseName, IProgress<int> progress = null, bool refresh = false) // returns new courseID
            {
                if (_Courses.ContainsKey(CourseName))
                    return _Courses[CourseName];
                else
                {
                    var C = await Patient.GetCourse(CourseName, progress);
                    if (C == null)
                    {
                        return null; // no such course
                    }
                    lock (CourseLoadLock)
                    {
                        if (!_Courses.ContainsKey(CourseName))
                        {
                            _Courses.Add(C.Id, C);
                            foreach (AsyncPlan P in C.Plans)
                            {
                                AddAsyncPlan(P);
                            }
                        }
                        else
                            return _Courses[CourseName];
                    }
                    return C;
                }
            }
            public static void ClearCourses()
            {
                _isCourseLoaded.Clear();
                _Courses.Clear();
                _AsyncPlans.Clear();
            }

            public static List<ProtocolView> GetProtocolList()
            {
                List<ProtocolView> previewlist = new List<ProtocolView>();
                using (var Context = new SquintdBModel())
                {
                    if (Context.DbLibraryProtocols != null)
                    {
                        List<DbLibraryProtocol> Protocols = Context.DbLibraryProtocols.Where(x => !x.isRetired).ToList();
                        foreach (DbLibraryProtocol DbP in Protocols)
                        {
                            Protocol P = new Protocol(DbP);
                            previewlist.Add(new ProtocolView(P));
                        }
                    }
                }
                return previewlist;
            }
            public static ProtocolView GetProtocolList(int Id)
            {
                ProtocolView PV = null;
                using (var Context = new SquintdBModel())
                {
                    if (Context.DbLibraryProtocols != null)
                    {
                        DbProtocol DbP = Context.DbLibraryProtocols.FirstOrDefault(x => x.ID == Id);
                        if (DbP != null)
                        {
                            Protocol P = new Protocol(DbP);
                            PV = new ProtocolView(P);
                        }
                    }
                }
                return PV;
            }

            public static void CreateNewProtocol()
            {
                CurrentProtocol = new Protocol();
            }
            public static void CloseProtocol()
            {
                ClearProtocolData();
                ProtocolLoaded = false;
            }
            public static Protocol LoadProtocol(string ProtocolName)
            {
                using (SquintdBModel Context = new SquintdBModel())
                {
                    if (ProtocolLoaded)
                    {
                        ClearProtocolData();
                        ProtocolLoaded = false;
                    }
                    DbProtocol DbP = Context.DbLibraryProtocols.Where(x => x.ProtocolName == ProtocolName && !x.isRetired).SingleOrDefault();
                    if (DbP == null)
                    {
                        ProtocolLoaded = false;
                        return null;
                    }
                    if (!areStructuresLoaded)
                    {
                        LoadStructures();
                        areStructuresLoaded = true;
                    }
                    try
                    {
                        CurrentProtocol = new Protocol(DbP);
                        CurrentSession.AddProtocol(CurrentProtocol);
                        bool AtLeastOneStructure = false;
                        var test = _ECSIDs;
                        foreach (DbECSID DbECSID in DbP.ECSIDs)
                        {
                            ECSID E = new ECSID(DbECSID);
                            AddECSID(E);
                            AtLeastOneStructure = true;
                        }
                        if (!AtLeastOneStructure)
                            new ECSID(Context.DbECSIDs.Find(1));  //Initialize non-defined structure
                        foreach (DbComponent DbC in DbP.Components)
                        {
                            Component SC = new Component(DbC);
                            AddComponent(SC);
                            foreach (DbComponentImaging DbCI in DbC.ImagingProtocols)
                            {
                                foreach (DbImaging I in DbCI.Imaging)
                                {
                                    SC.ImagingProtocolsAttached.Add((ImagingProtocols)I.ImagingProtocol);
                                }
                            }
                            if (DbC.Checklists != null)
                            {
                                var DbChecklist = DbC.Checklists.FirstOrDefault(x => x.ProtocolDefault == false);
                                if (DbChecklist != null)
                                    SC.Checklist = new ComponentChecklist(DbChecklist);
                                else
                                {
                                    DbChecklist = DbC.Checklists.FirstOrDefault(x => x.ProtocolDefault == true);
                                    SC.Checklist = new ComponentChecklist(DbChecklist);
                                }
                            }
                            if (DbC.Constraints != null)
                            {
                                foreach (DbConstraint DbCon in DbC.Constraints)
                                {
                                    Constraint Con = new Constraint(DbCon); // starts tracking
                                    AddConstraint(Con);
                                    if (DbCon.DbConThresholds != null) // ensure there is at least a major violation defined.
                                        if (DbCon.DbConThresholds.Count > 0)
                                            foreach (DbConThreshold DbConThresh in DbCon.DbConThresholds)
                                            {
                                                ConstraintThreshold CT = LoadConstraintThreshold(DbConThresh, Con.ID);
                                            }
                                        else
                                        {
                                            DbConThresholdDef MajorViolation = SquintDb.Context.DbConThresholdDefs.Where(x => x.Threshold == (int)ConstraintThresholdNames.MajorViolation).Single();
                                            ConstraintThreshold CT = new ConstraintThreshold(MajorViolation, Con, Con.ReferenceValue);
                                            AddConstraintThreshold(CT);
                                        }
                                    else
                                    {
                                        throw new NotImplementedException();
                                        //DbConThresholdDef MajorViolation = SquintDb.Context.DbConThresholdDefs.Where(x => x.Threshold == (int)ConstraintThresholdNames.MajorViolation).Single();
                                        //CT = new ConstraintThreshold(MajorViolation, _DbO.ID, _DbO.ReferenceValue);
                                        //CT.ConstraintThresholdChanged += OnConstraintThresholdChanged;
                                    }
                                }
                            }
                        }
                        return CurrentProtocol;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format("{0}/r/n{1}/r/n{2}", ex.Message, ex.InnerException, ex.StackTrace));
                        return null;
                    }
                }
            }
            private static void ClearProtocolData()
            {
                foreach (ECSID E in _ECSIDs.Values.ToList())
                    E.Delete();
                foreach (Constraint C in _Constraints.Values.ToList())
                    C.Delete();
                foreach (Component Comp in _Components.Values.ToList())
                    Comp.Delete();
                //foreach (Constituent Cons in _Constituents.Values.ToList())
                //    Cons.Delete();
                foreach (ECPlan P in _Plans.Values.ToList())
                    P.Delete();
                CurrentProtocol = null;
            }
            public static void Load_Session(int ID)
            {
                using (SquintdBModel Context = new SquintdBModel())
                {
                    DbSession DbS = Context.DbSessions.FirstOrDefault(x => x.PID == PatientID && x.ID == ID);
                    if (DbS == null)
                        return;
                    DbSessionProtocol DbSP = DbS.DbSessionProtocol;
                    if (DbSP == null)
                    {
                        ProtocolLoaded = false;
                        return;
                    }
                    if (ProtocolLoaded)
                    {
                        ClearProtocolData();
                        ProtocolLoaded = false;
                    }
                    if (!areStructuresLoaded)
                    {
                        LoadStructures();
                        areStructuresLoaded = true;
                    }
                    try
                    {
                        CurrentProtocol = new Protocol(DbSP);
                        CurrentSession.AddProtocol(CurrentProtocol);
                        bool AtLeastOneStructure = false;
                        var test = _ECSIDs;
                        foreach (DbSessionECSID DbECSID in DbSP.ECSIDs)
                        {
                            ECSID E = new ECSID(DbECSID);
                            AddECSID(E);
                            AtLeastOneStructure = true;
                        }
                        if (!AtLeastOneStructure)
                            new ECSID(Context.DbECSIDs.Find(1));  //Initialize non-defined structure
                        foreach (DbSessionComponent DbC in DbSP.Components)
                        {
                            Component SC = new Component(DbC);
                            AddComponent(SC); foreach (DbComponentImaging DbCI in DbC.ImagingProtocols)
                            {
                                foreach (DbImaging I in DbCI.Imaging)
                                {
                                    SC.ImagingProtocolsAttached.Add((ImagingProtocols)I.ImagingProtocol);
                                }
                            }
                            if (DbC.Checklists != null)
                            {
                                var DbChecklist = DbC.Checklists.FirstOrDefault(x => x.ProtocolDefault == false);
                                if (DbChecklist != null)
                                    SC.Checklist = new ComponentChecklist(DbChecklist);
                                else
                                {
                                    DbChecklist = DbC.Checklists.FirstOrDefault(x => x.ProtocolDefault == true);
                                    SC.Checklist = new ComponentChecklist(DbChecklist);
                                }
                            }
                            if (DbC.Constraints != null)
                            {
                                foreach (DbSessionConstraint DbCon in DbC.Constraints)
                                {
                                    Constraint Con = new Constraint(DbCon); // starts tracking
                                    AddConstraint(Con);
                                    if (DbCon.DbConThresholds != null) // ensure there is at least a major violation defined.
                                        if (DbCon.DbConThresholds.Count > 0)
                                            foreach (DbConThreshold DbConThresh in DbCon.DbConThresholds)
                                            {
                                                ConstraintThreshold CT = LoadConstraintThreshold(DbConThresh, Con.ID);
                                            }
                                        else
                                        {
                                            DbConThresholdDef MajorViolation = SquintDb.Context.DbConThresholdDefs.Where(x => x.Threshold == (int)ConstraintThresholdNames.MajorViolation).Single();
                                            ConstraintThreshold CT = new ConstraintThreshold(MajorViolation, Con, Con.ReferenceValue);
                                            AddConstraintThreshold(CT);
                                        }
                                    else
                                    {
                                        throw new NotImplementedException();
                                        //DbConThresholdDef MajorViolation = SquintDb.Context.DbConThresholdDefs.Where(x => x.Threshold == (int)ConstraintThresholdNames.MajorViolation).Single();
                                        //CT = new ConstraintThreshold(MajorViolation, _DbO.ID, _DbO.ReferenceValue);
                                        //CT.ConstraintThresholdChanged += OnConstraintThresholdChanged;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format("{0}/r/n{1}/r/n{2}", ex.Message, ex.InnerException, ex.StackTrace));
                        return;
                    }
                }
            }
            public static List<Session> GetSessions()
            {
                List<Session> S = new List<Session>();
                using (SquintdBModel Context = new SquintdBModel())
                {
                    foreach (DbSession DbS in Context.DbSessions.Where(x => x.PID == PatientID).ToList())
                    {
                        S.Add(new Session(DbS));
                    }
                }
                return S;
            }
            public static void Delete_Session(int ID)
            {
                using (SquintdBModel Context = new SquintdBModel())
                {
                    DbSession DbS = Context.DbSessions.Find(ID);
                    if (DbS != null)
                    {
                        Context.DbSessions.Remove(DbS);
                        Context.SaveChanges();
                    }
                }
            }
            public static void Save_Session(string SessionComment)
            {
                Dictionary<int, int> ComponentLookup = new Dictionary<int, int>();
                Dictionary<int, int> StructureLookup = new Dictionary<int, int>();
                Dictionary<int, int> AssessmentLookup = new Dictionary<int, int>();
                using (SquintdBModel Context = new SquintdBModel())
                {
                    // Create session
                    DbSession DbS = Context.DbSessions.Create();
                    Context.DbSessions.Add(DbS);
                    DbS.ID = IDGenerator();
                    DbS.PID = PatientID;
                    DbS.SessionComment = SessionComment;
                    DbS.SessionCreator = SquintUser;
                    DbS.SessionDateTime = string.Format("{0} {1}", DateTime.Now.ToShortDateString(), DateTime.Now.ToShortTimeString());
                    //Create Session Protocol
                    DbSessionProtocol DbP = Context.DbSessionProtocols.Create();
                    Context.DbSessionProtocols.Add(DbP);
                    DbS.DbSessionProtocol = DbP;
                    DbP.ID = IDGenerator();
                    DbP.ProtocolName = CurrentProtocol.ProtocolName;
                    DbP.ApproverID = 1;
                    DbP.AuthorID = 1;
                    DbP.TreatmentCentreID = Context.DbTreatmentCentres.FirstOrDefault(x => x.TreatmentCentre == (int)CurrentProtocol.TreatmentCentre).ID;
                    DbP.TreatmentSiteID = Context.DbTreatmentSites.FirstOrDefault(x => x.TreatmentSite == (int)CurrentProtocol.TreatmentSite).ID;
                    DbP.ApprovalLevelID = Context.DbApprovalLevels.FirstOrDefault(x => x.ApprovalLevel == (int)ApprovalLevels.Unapproved).ID;
                    DbP.ProtocolTypeID = Context.DbProtocolTypes.FirstOrDefault(x => x.ProtocolType == (int)CurrentProtocol.ProtocolType).ID;
                    DbP.LastModifiedBy = SquintUser;
                    DbP.LastModifiedDate = DateTime.Now.ToShortDateString();
                    DbP.ProtocolParentID = CurrentProtocol.ID;
                    // Assessment
                    foreach (Assessment A in _Assessments.Values)
                    {
                        DbAssessment DbA = Context.DbAssessments.Create();
                        Context.DbAssessments.Add(DbA);
                        DbA.ID = IDGenerator();
                        AssessmentLookup.Add(A.ID, DbA.ID);
                        DbA.SessionID = DbS.ID;
                        DbA.PID = Ctr.PatientID;
                        DbA.PatientName = string.Format("{0}, {1}", Ctr.PatientLastName, Ctr.PatientFirstName);
                        DbA.DateOfAssessment = DateTime.Now.ToShortDateString();
                        DbA.SquintUser = Ctr.SquintUser;
                        DbA.AssessmentName = A.AssessmentName;
                        //Update Components
                    }
                    var SessionECSID_Lookup = new Dictionary<int, int>();
                    foreach (ECSID S in _ECSIDs.Values)
                    {
                        DbSessionECSID DbE = Context.DbSessionECSIDs.Create();
                        Context.DbSessionECSIDs.Add(DbE);
                        DbE.ID = IDGenerator();
                        SessionECSID_Lookup.Add(S.ID, DbE.ID);
                        DbE.SessionId = DbS.ID;
                        DbE.ParentECSID_Id = S.ID;
                        DbE.AssignedEclipseId = S.EclipseStructureName;
                        StructureLookup.Add(S.ID, DbE.ID);
                        DbE.ProtocolID = DbP.ID;
                        DbE.ProtocolStructureName = S.ProtocolStructureName;
                        DbE.StructureLabelID = S.StructureLabelID;
                        DbE.DisplayOrder = S.DisplayOrder;
                        DbE.DefaultEclipseAliases = String.Join(";", S.DefaultEclipseAliases);
                        // Create new checklist
                        DbStructureChecklist DbSC = Context.DbStructureChecklists.Create();
                        Context.DbStructureChecklists.Add(DbSC);
                        DbSC.isPointContourChecked = S.CheckList.isPointContourChecked;
                        DbSC.PointContourThreshold = S.CheckList.PointContourVolumeThreshold;
                        DbE.DbStructureChecklist = DbSC;
                    }
                    foreach (Component SC in _Components.Values)
                    {
                        DbSessionComponent DbC = Context.DbSessionComponents.Create();
                        Context.DbSessionComponents.Add(DbC);
                        DbC.ID = IDGenerator();
                        DbC.SessionID = DbS.ID;
                        DbC.ParentComponentID = SC.ID;
                        ComponentLookup.Add(SC.ID, DbC.ID);
                        DbC.ProtocolID = DbP.ID;
                        //Update
                        DbC.ComponentName = SC.ComponentName;
                        DbC.NumFractions = SC.NumFractions;
                        DbC.ReferenceDose = SC.ReferenceDose;
                        //Add component checklist
                        if (SC.Checklist != null)
                        {
                            var C = SC.Checklist;
                            var DbCC = Context.DbComponentChecklists.Create();
                            Context.DbComponentChecklists.Add(DbCC);
                            DbCC.DbComponent = DbC;
                            DbCC.TreatmentTechniqueType = (int)C.TreatmentTechniqueType;
                            DbCC.MinFields = C.MinFields;
                            DbCC.MaxFields = C.MaxFields;
                            DbCC.MinMU = C.MinMU;
                            DbCC.MaxMU = C.MaxMU;
                            DbCC.VMAT_MinColAngle = C.VMAT_MinColAngle;
                            DbCC.VMAT_MinFieldColSeparation = C.VMAT_MinFieldColSeparation;
                            DbCC.NumIso = C.NumIso;
                            DbCC.VMAT_SameStartStop = C.VMAT_SameStartStop;
                            DbCC.MinXJaw = C.MinXJaw;
                            DbCC.MaxXJaw = C.MaxXJaw;
                            DbCC.MinYJaw = C.MinYJaw;
                            DbCC.MaxYJaw = C.MaxYJaw;
                            DbCC.VMAT_JawTracking = (int)C.VMAT_JawTracking;
                            DbCC.Algorithm = (int)C.Algorithm;
                            DbCC.FieldNormalizationMode = (int)C.FieldNormalizationMode;
                            DbCC.AlgorithmResolution = C.AlgorithmResolution;
                            DbCC.PNVMin = C.PNVMin;
                            DbCC.PNVMax = C.PNVMax;
                            DbCC.SliceSpacing = C.SliceSpacing;
                            DbCC.HeterogeneityOn = C.HeterogeneityOn;
                            //Couch
                            DbCC.SupportIndication = (int)C.SupportIndication;
                            DbCC.CouchSurface = C.CouchSurface;
                            DbCC.CouchInterior = C.CouchInterior;
                            //Bolus
                            DbCC.BolusClinicalIndication = (int)C.BolusClinicalIndication;
                            DbCC.BolusClinicalHU = C.BolusClinicalHU;
                            DbCC.BolusClinicalThickness = C.BolusClinicalThickness;
                            //Artifact
                            foreach (Artifact A in C.Artifacts)
                            {
                                DbArtifact DbA = Context.DbArtifacts.Create();
                                Context.DbArtifacts.Add(DbA);
                                DbA.DbComponentChecklist = DbCC;
                                DbA.ECSID_ID = SessionECSID_Lookup[A.E.ID]; // will have been duplicated above;
                                DbA.HU = A.CheckHU;
                                DbA.DbComponentChecklist = DbCC;
                            }
                        }
                    }
                    foreach (Constraint Con in _Constraints.Values)
                    {
                        DbSessionConstraint DbO = Context.DbSessionConstraints.Create();
                        Context.DbSessionConstraints.Add(DbO);
                        DbO.ID = IDGenerator();
                        DbO.SessionID = DbS.ID;
                        DbO.ParentConstraintID = Con.ID;
                        DbO.ComponentID = ComponentLookup[Con.ComponentID];
                        // Update
                        DbO.PrimaryStructureID = StructureLookup[Con.PrimaryStructureID];
                        DbO.ReferenceScale = (int)Con.ReferenceScale;
                        DbO.ReferenceType = (int)Con.ReferenceType;
                        DbO.ReferenceValue = Con.ReferenceValue;
                        if (Con.SecondaryStructureID == 1)
                            DbO.SecondaryStructureID = 1;
                        else
                            DbO.SecondaryStructureID = StructureLookup[Con.SecondaryStructureID];
                        DbO.ConstraintScale = (int)Con.ConstraintScale;
                        DbO.ConstraintType = (int)Con.ConstraintType;
                        DbO.ConstraintValue = Con.ConstraintValue;
                        DbO.DisplayOrder = Con.DisplayOrder;
                        DbO.Fractions = Con.NumFractions;
                        //Link Results to Asssessment
                        foreach (Assessment A in _Assessments.Values)
                        {
                            ConstraintResultView CRV = Con.GetResult(A.ID);
                            if (CRV != null)
                            {
                                DbConstraintResult DbCR = Context.DbConstraintResults.Create();
                                Context.DbConstraintResults.Add(DbCR);
                                DbCR.AssessmentID = AssessmentLookup[A.ID];
                                DbCR.SessionConstraintID = DbO.ID;
                                DbCR.ResultString = CRV.Result;
                                DbCR.ResultValue = CRV.ResultValue;
                            }
                        }
                    }
                    try
                    {
                        Context.SaveChanges();
                    }
                    catch (Exception ex)
                    {

                        MessageBox.Show(string.Format("{0} {1} {2}", ex.Message, ex.InnerException.InnerException, ex.StackTrace));
                    }
                }
            }
            public static void Save_UpdateProtocol()
            {
                using (SquintdBModel Context = new SquintdBModel())
                {
                    //Update Protocol
                    DbProtocol DbP = Context.DbLibraryProtocols.Find(CurrentProtocol.ID);
                    DbP.TreatmentCentreID = Context.DbTreatmentCentres.FirstOrDefault(x => x.TreatmentCentre == (int)CurrentProtocol.TreatmentCentre).ID;
                    DbP.TreatmentSiteID = Context.DbTreatmentSites.FirstOrDefault(x => x.TreatmentSite == (int)CurrentProtocol.TreatmentSite).ID;
                    DbP.ProtocolTypeID = Context.DbProtocolTypes.FirstOrDefault(x => x.ProtocolType == (int)CurrentProtocol.ProtocolType).ID;
                    DbP.ApprovalLevelID = Context.DbApprovalLevels.FirstOrDefault(x => x.ApprovalLevel == (int)CurrentProtocol.ApprovalLevel).ID;
                    DbP.LastModifiedBy = SquintUser;
                    DbP.LastModifiedDate = DateTime.Now.ToShortDateString();
                    DbP.ProtocolName = CurrentProtocol.ProtocolName;
                    //Update Components
                    foreach (Component SC in _Components.Values)
                    {
                        if (SC.ID > 0)
                        {
                            DbComponent DbC = Context.DbComponents.Find(SC.ID);
                            //Update
                            DbC.ComponentName = SC.ComponentName;
                            DbC.NumFractions = SC.NumFractions;
                            DbC.ReferenceDose = SC.ReferenceDose;
                        }
                        else
                        {
                            DbComponent DbC = Context.DbComponents.Create();
                            Context.DbComponents.Add(DbC);
                            DbC.ID = SC.ID;
                            //Update
                            DbC.ComponentName = SC.ComponentName;
                            DbC.NumFractions = SC.NumFractions;
                            DbC.ReferenceDose = SC.ReferenceDose;
                        }
                    }

                    foreach (Constraint Con in _Constraints.Values)
                    {
                        DbConstraint DbO;
                        if (Con.isCreated)
                        {
                            DbO = Context.DbConstraints.Create();
                            Context.DbConstraints.Add(DbO);
                            DbO.ID = Con.ID;
                            // Update
                            DbO.PrimaryStructureID = Con.PrimaryStructureID;
                            DbO.ReferenceScale = (int)Con.ReferenceScale;
                            DbO.ReferenceType = (int)Con.ReferenceType;
                            DbO.ReferenceValue = Con.ReferenceValue;
                            DbO.SecondaryStructureID = Con.SecondaryStructureID;
                            DbO.ComponentID = Con.ComponentID;
                            DbO.ConstraintScale = (int)Con.ConstraintScale;
                            DbO.ConstraintType = (int)Con.ConstraintType;
                            DbO.ConstraintValue = Con.ConstraintValue;
                            DbO.DisplayOrder = Con.DisplayOrder;
                            DbO.Fractions = Con.NumFractions;
                        }
                        else
                        {
                            DbO = Context.DbConstraints.Find(Con.ID);
                            // Update
                            DbO.PrimaryStructureID = Con.PrimaryStructureID;
                            DbO.ReferenceScale = (int)Con.ReferenceScale;
                            DbO.ReferenceType = (int)Con.ReferenceType;
                            DbO.ReferenceValue = Con.ReferenceValue;
                            DbO.SecondaryStructureID = Con.SecondaryStructureID;
                            DbO.ComponentID = Con.ComponentID;
                            DbO.ConstraintScale = (int)Con.ConstraintScale;
                            DbO.ConstraintType = (int)Con.ConstraintType;
                            DbO.ConstraintValue = Con.ConstraintValue;
                            DbO.DisplayOrder = Con.DisplayOrder;
                            DbO.Fractions = Con.NumFractions;
                        }
                    }
                    try
                    {
                        Context.SaveChanges();
                        CurrentProtocol.LastModifiedBy = SquintUser;
                    }
                    catch (Exception ex)
                    {
                        string debugme = "hi";
                    }
                }
            }
            public static void Save_DuplicateProtocol()
            {
                using (SquintdBModel Context = new SquintdBModel())
                {
                    // Name check
                    if (Context.DbLibraryProtocols.Select(x => x.ProtocolName).Contains(CurrentProtocol.ProtocolName))
                        CurrentProtocol.ProtocolName = CurrentProtocol.ProtocolName + "_copy";
                    //Duplicate Protocol
                    DbLibraryProtocol DbP = Context.DbLibraryProtocols.Create();
                    Context.DbLibraryProtocols.Add(DbP);
                    DbP.ID = IDGenerator();
                    DbP.ProtocolName = CurrentProtocol.ProtocolName;
                    DbP.ApproverID = 1;
                    DbP.AuthorID = 1;
                    DbP.TreatmentCentreID = Context.DbTreatmentCentres.FirstOrDefault(x => x.TreatmentCentre == (int)CurrentProtocol.TreatmentCentre).ID;
                    DbP.TreatmentSiteID = Context.DbTreatmentSites.FirstOrDefault(x => x.TreatmentSite == (int)CurrentProtocol.TreatmentSite).ID;
                    DbP.ApprovalLevelID = Context.DbApprovalLevels.FirstOrDefault(x => x.ApprovalLevel == (int)ApprovalLevels.Unapproved).ID;
                    DbP.ProtocolTypeID = Context.DbProtocolTypes.FirstOrDefault(x => x.ProtocolType == (int)CurrentProtocol.ProtocolType).ID;
                    DbP.LastModifiedBy = SquintUser;
                    DbP.LastModifiedDate = DateTime.Now.ToShortDateString();
                    DbP.ProtocolParentID = CurrentProtocol.ID;
                    //Update Components
                    Dictionary<int, int> ComponentLookup = new Dictionary<int, int>();
                    Dictionary<int, int> StructureLookup = new Dictionary<int, int>();
                    foreach (ECSID S in _ECSIDs.Values)
                    {
                        DbECSID DbE = Context.DbECSIDs.Create();
                        Context.DbECSIDs.Add(DbE);
                        DbE.ID = IDGenerator();
                        StructureLookup.Add(S.ID, DbE.ID);
                        DbE.ProtocolID = DbP.ID;
                        DbE.ProtocolStructureName = S.ProtocolStructureName;
                        DbE.StructureLabelID = S.StructureLabelID;
                        DbE.DisplayOrder = S.DisplayOrder;
                        DbE.DefaultEclipseAliases = String.Join(";", S.DefaultEclipseAliases);
                    }
                    foreach (Component SC in _Components.Values)
                    {
                        DbComponent DbC = Context.DbComponents.Create();
                        Context.DbComponents.Add(DbC);
                        DbC.ID = IDGenerator();
                        ComponentLookup.Add(SC.ID, DbC.ID);
                        DbC.ProtocolID = DbP.ID;
                        //Update
                        DbC.ComponentName = SC.ComponentName;
                        DbC.NumFractions = SC.NumFractions;
                        DbC.ReferenceDose = SC.ReferenceDose;
                    }
                    foreach (Constraint Con in _Constraints.Values)
                    {
                        DbConstraint DbO = Context.DbConstraints.Create();
                        Context.DbConstraints.Add(DbO);
                        DbO.ID = IDGenerator();
                        DbO.ComponentID = ComponentLookup[Con.ComponentID];
                        // Update
                        DbO.PrimaryStructureID = StructureLookup[Con.PrimaryStructureID];
                        DbO.ReferenceScale = (int)Con.ReferenceScale;
                        DbO.ReferenceType = (int)Con.ReferenceType;
                        DbO.ReferenceValue = Con.ReferenceValue;
                        if (Con.SecondaryStructureID == 1)
                            DbO.SecondaryStructureID = 1;
                        else
                            DbO.SecondaryStructureID = StructureLookup[Con.SecondaryStructureID];
                        DbO.ConstraintScale = (int)Con.ConstraintScale;
                        DbO.ConstraintType = (int)Con.ConstraintType;
                        DbO.ConstraintValue = Con.ConstraintValue;
                        DbO.DisplayOrder = Con.DisplayOrder;
                        DbO.Fractions = Con.NumFractions;
                    }
                    try
                    {
                        Context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        string debugme = "hi";
                    }
                }
            }
            public static bool Delete_Protocol(int Id)
            {
                using (var Context = new SquintdBModel())
                {
                    var DbP = Context.DbLibraryProtocols.Find(Id);
                    if (DbP != null)
                    {
                        Context.Entry(DbP).State = System.Data.Entity.EntityState.Deleted;
                        Context.SaveChanges();
                        return true;
                    }
                    else
                        return false;
                }

            }
        }
    }
}