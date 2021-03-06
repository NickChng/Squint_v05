﻿using System;
using System.Linq;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Threading;
using PropertyChanged;
using System.Windows.Controls;
using System.Windows;
using wpfcolors = System.Windows.Media.Colors;
using wpfcolor = System.Windows.Media.Color;
using wpfbrush = System.Windows.Media.SolidColorBrush;
using Controls = SquintScript.Controls;


namespace SquintScript
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChangedEvent([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DelegateCommand : ICommand
    {
        private readonly Action<object> _action;

        public DelegateCommand(Action<object> action)
        {
            _action = action;
        }

        public void Execute(object parameter)
        {
            _action(parameter);
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67
    }
    public enum ViewEnums
    {
        [Description("Centre:")] Centre,
        [Description("Approval:")] Approval,
        [Description("Protocol type:")] ProtocolType,
        [Description("Approver:")] Approver,
        [Description("Site:")] Site,

    }

    //[AddINotifyPropertyChangedInterface]
    public class StructureProperties
    {
        public bool isBeingEdited { get; set; } = false;
        public string Label { get; set; } = "Label";
        public string abRatio { get; set; } = "-1";
    }

    public class StructureSelector : ObservableObject
    {
        private bool isSelected { get; set; }
        private Ctr.StructureView SV;
        public int Id
        {
            get { return SV.ID; }
        }
        public bool isUserAdded
        {
            get
            {
                if (SV.ID < 0)
                    return true;
                else
                    return false;
            }
        }
        public string ProtocolStructureName
        {
            get { return SV.ProtocolStructureName; }
            set
            {
                if (value.Length > 1)
                {
                    SV.ProtocolStructureName = value;
                    RaisePropertyChangedEvent("StructureId");
                }
                else MessageBox.Show("Structure name must be greater than 1 character");
            }
        }
        public string EclipseStructureName
        {
            get { return EclipseStructure.Id; }
        }
        public Ctr.EclipseStructure EclipseStructure
        {
            get { return SV.EclipseStructure; }
            set
            {
                if (value != null)
                    SV.EclipseStructure = value;
            }
        }
        public List<string> GetAliases()
        {
            return SV.GetAliases();
        }
        public string LabelName
        {
            get { return SV.StructureLabel.LabelName; }
        }
        public string AlphaBetaRatio
        {
            get
            {
                if (SV.StructureLabel.AlphaBetaRatio > 0.1)
                    return string.Format(@"({0}\{1} = {2:0})", '\u03B1', '\u03B2', SV.StructureLabel.AlphaBetaRatio);
                else
                    return string.Format(@"(No BED adjustment)");
            }
        }
        public bool LabelIsConsistent
        {
            get
            {
                if (SV.EclipseStructure.LabelName.ToUpper() == SV.StructureLabel.LabelName.ToUpper() || SV.EclipseStructure.Id == "")
                {
                    return false;
                }
                else
                    return true;
            }
        }
        public string LabelMismatchTooltip
        {
            get
            {
                return string.Format("Label mismatch: Assigned structure label is {0}", SV.EclipseStructure.LabelName);
            }
        }
        public StructureSelector(Ctr.StructureView SVin)
        {
            SV = SVin;
            SV.StructureChanged += OnStructureViewChanged;
        }
        private void OnStructureViewChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChangedEvent(e.PropertyName);
            if (e.PropertyName == "ES" || e.PropertyName == "LabelName")
            {
                RaisePropertyChangedEvent("EclipseStructureName"); // this is for when the Assigned Eclipse structure itself is changed
                RaisePropertyChangedEvent("LabelIsConsistent"); // this is for when the Assigned Eclipse structure itself is changed
                RaisePropertyChangedEvent("LabelMismatchTooltip");
            }
        }
        public ObservableCollection<StructureProperties> test { get; set; } = new ObservableCollection<StructureProperties>() { new StructureProperties() };
    }
    [AddINotifyPropertyChangedInterface]
    public class ComponentSelector : ObservableObject
    {
        private bool isSelected { get; set; }
        public bool Pinned { get; set; } = false;
        public int DisplayHeight { get; } = 100;
        private Ctr.ComponentView CompV;
        public int Id
        {
            get { return CompV.ID; }
        }
        public string ComponentName
        {
            get { return CompV.ComponentName; }
            set { CompV.ComponentName = value; }
        }
        public ComponentTypes ComponentType
        {
            get { return CompV.ComponentType; }
            set { }
        }
        public double ReferenceDose
        {
            get { return CompV.ReferenceDose; }
            set
            {
                CompV.ReferenceDose = value;
                RaisePropertyChangedEvent("ComponentTDFDescription"); // Fody will update the primary property, this is to update the description
            }
        }
        public int NumFractions
        {
            get { return CompV.NumFractions; }
            set
            {
                SetFractions(value);
                RaisePropertyChangedEvent("ComponentTDFDescription"); // Fody will update the primary property, this is to update the description
            }
        }
        public string ComponentTDFDescription
        {
            get { return string.Format("({0:0.#} Gy in {1} fractions)", ReferenceDose / 100, NumFractions); }
        }
        public ComponentSelector(Ctr.ComponentView CompVin)
        {
            CompV = CompVin;
            AvailableComponentTypes.Clear();
            foreach (ComponentTypes T in Enum.GetValues(typeof(ComponentTypes)))
            {
                AvailableComponentTypes.Add(T);
            }
        }
        private async void SetFractions(int NumFractions)
        {
            await CompV.SetFractionsAsync(NumFractions);
        }
        public ObservableCollection<ComponentTypes> AvailableComponentTypes { get; set; } = new ObservableCollection<ComponentTypes>() { ComponentTypes.Plan };
    }

    [AddINotifyPropertyChangedInterface]
    public class ConstraintSelector : ObservableObject
    {
        //        public event PropertyChangedEventHandler PropertyChanged;
        public bool RefreshFlag { get; set; } // this is the ID of the assessment to update
        public bool RefreshRowHeader { get; set; }
        public bool ConstraintInfoVisibility { get; set; } = false;
        public string ChangeDescription
        {
            get
            {
                return CV.ChangeDescription;
            }
            set
            {
                CV.ChangeDescription = value;
            }
        } // only used for entry when users changes constraint within the AdminWindow.
        public ObservableCollection<Ctr.ConstraintChangelog> ConstraintChangelogs
        {
            get
            {
                if (CV == null)
                {
                    return new ObservableCollection<Ctr.ConstraintChangelog>() { new Ctr.ConstraintChangelog(), new Ctr.ConstraintChangelog() };
                }
                else
                {
                    return new ObservableCollection<Ctr.ConstraintChangelog>(CV.GetChangeLogs());
                }
            }
        }
        private Ctr.ConstraintView CV;
        private StructureSelector _SS;
        public StructureSelector SS
        {
            get
            {
                return _SS;
            }
            set
            {
                if (_SS != value)
                {
                    _SS = value;
                    CV.PrimaryStructureID = _SS.Id;
                }
            }
        }
        public int Id
        {
            get { return CV.ID; }
        }
        public bool Pinned { get; set; } = false;
        public wpfcolor Color { get; set; } = wpfcolors.PapayaWhip;
        public ComponentSelector Component
        {
            get { return Components.FirstOrDefault(x => x.Id == CV.ComponentID); }
            set { CV.ComponentID = value.Id; }
        }
        public string ComponentName
        {
            get { return CV.ComponentName; }
            set { }
        }
        public int DisplayOrder
        {
            get { return CV.DisplayOrder; }
            set { CV.DisplayOrder = value; }
        }
        public double ConstraintValue
        {
            get { return CV.ConstraintValue; }
            set { CV.ConstraintValue = value; RaisePropertyChangedEvent(nameof(ConstraintValueColor)); }
        }
        public wpfbrush ConstraintValueColor
        {
            get
            {
                if (CV.isModified(nameof(CV.ConstraintValue)) && !CV.isCreated)
                    return new wpfbrush(wpfcolors.DarkOrange);
                else
                    return new wpfbrush(wpfcolors.Black);
            }
        }
        public double ReferenceValue
        {
            get { return CV.ReferenceValue; }
            set
            {
                CV.ReferenceValue = value;
                RaisePropertyChangedEvent(nameof(ReferenceValueColor));
                RaisePropertyChangedEvent(nameof(RefreshFlag));
            }
        }
        public wpfbrush ReferenceValueColor
        {
            get
            {
                if (CV.isModified(nameof(CV.ReferenceValue)) && !CV.isCreated)
                    return new wpfbrush(wpfcolors.DarkOrange);
                else
                    return new wpfbrush(wpfcolors.Black);
            }
        }
        public double StopValue
        {
            get
            {
                return CV.StopValue;
            }
            set
            {
                CV.StopValue = value;
                RefreshFlag = !RefreshFlag;
            }
        }
        public double MinorViolation
        {
            get
            {
                return CV.MinorViolation;
            }
            set
            {
                CV.MinorViolation = value;
                RaisePropertyChangedEvent("RefreshFlag");
            }
        }
        public double MajorViolation
        {
            get
            {
                return CV.MajorViolation;
            }
            set
            {
                CV.MajorViolation = value;
                RefreshFlag = !RefreshFlag;
            }
        }
        public ConstraintTypeCodes ConstraintType
        {
            get { return CV.ConstraintType; }
            set
            {
                CV.ConstraintType = value;
                SetComboBoxes();
            }
        }
        public ConstraintUnits ConstraintUnit
        {
            get { return CV.ConstraintUnit; }
            set
            {
                if (value != CV.ConstraintUnit)
                {
                    switch (value)
                    {
                        case ConstraintUnits.Multiple:
                            CV.ConstraintScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.cc:
                            CV.ConstraintScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.cGy:
                            CV.ConstraintScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.Percent:
                            CV.ConstraintScale = UnitScale.Relative;
                            break;
                    }
                }
            }
        }
        public ConstraintUnits ReferenceUnit
        {
            get { return CV.ReferenceUnit; }
            set
            {
                if (value != CV.ReferenceUnit)
                {
                    switch (value)
                    {
                        case ConstraintUnits.Multiple:
                            CV.ReferenceScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.cc:
                            CV.ReferenceScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.cGy:
                            CV.ReferenceScale = UnitScale.Absolute;
                            break;
                        case ConstraintUnits.Percent:
                            CV.ReferenceScale = UnitScale.Relative;
                            break;
                    }
                }
            }
        }
        public List<System.Windows.Media.Color> GetStructureColors
        {
            get { return CV.StructureColors; }
            set { }
        }
        public string StructureId
        {
            get { return CV.PrimaryStructureName; }
        }
        public string FullConstraintDefinition
        {
            get
            {
                return CV.ConstraintDefinition;
            }
        }
        public string ShortConstraintDefinition
        {
            get { return CV.ShortConstraintDefinition; }
        }
        public string IsAddedStatus
        {
            get
            {
                if (Id < 0)
                    return "New";
                else
                    return "";
            }
        }
        public string ChangeStatusString
        {
            get
            {
                if (CV.isModified() && !CV.isCreated)
                    return ChangeStatus.Modified.Display();
                else
                    return "";
            }
        }
        public bool isModified
        {
            get
            {
                return (CV.isModified());
            }
        }
        public ReferenceTypes ReferenceType
        {
            get { return CV.ReferenceType; }
            set
            {
                if (value != CV.ReferenceType)
                {
                    CV.ReferenceType = value;
                    RaisePropertyChangedEvent("OppositeReferenceTypeString"); // notify view to update the opposite as well
                }
            }
        }
        public string OppositeReferenceTypeString
        {
            get
            {
                if (CV.ReferenceType == ReferenceTypes.Lower)
                    return "<";
                if (CV.ReferenceType == ReferenceTypes.Upper)
                    return ">";
                else
                    return "";
            }
        }
        public string GetResult(int AssessmentId)
        {
            Ctr.ConstraintResultView CRV = CV.GetResult(AssessmentId);
            if (CRV != null)
                return CRV.Result;
            else
                return "";
        }
        public List<ConstraintResultStatusCodes> GetStatusCodes(int AssessmentId)
        {
            Ctr.ConstraintResultView CRV = CV.GetResult(AssessmentId);
            if (CRV != null)
                return CRV.StatusCodes;
            else
                return null;
        }
        public bool isResultCalculating(int AssessmentId)
        {
            Ctr.ConstraintResultView CRV = CV.GetResult(AssessmentId);
            if (CRV != null)
                return CRV.isCalculating;
            else
                return false;
        }
        public ConstraintThresholdNames GetViolationStatus(int AssessmentId)
        {
            Ctr.ConstraintResultView CRV = CV.GetResult(AssessmentId);
            if (CRV != null)
                return CRV.ThresholdStatus;
            else
                return ConstraintThresholdNames.Unset;
        }
        private void SetComboBoxes()
        {
            ConstraintUnitTypes.Clear();
            foreach (ConstraintUnits U in CV.GetAvailableConstraintUnitList())
            {
                ConstraintUnitTypes.Add(U);
            }
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ConstraintUnits"));
            RaisePropertyChangedEvent("ConstraintUnits");
            AvailableReferenceUnitTypes.Clear();
            foreach (ConstraintUnits U in CV.GetAvailableReferenceUnitList())
            {
                AvailableReferenceUnitTypes.Add(U);
            }
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReferenceUnits"));
            RaisePropertyChangedEvent("ReferenceUnits");
            AvailableReferenceTypes.Clear();
            foreach (ReferenceTypes RT in Enum.GetValues(typeof(ReferenceTypes)).Cast<ReferenceTypes>())
            {
                if (RT == ReferenceTypes.Unset)
                    continue; // don't list this
                AvailableReferenceTypes.Add(RT);
            }
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReferenceTypes"));
            RaisePropertyChangedEvent("ReferenceTypes");
        }
        public ConstraintSelector(Ctr.ConstraintView CVin, StructureSelector SSin)
        {
            CV = CVin;
            SS = SSin;
            ConstraintTypes.Clear();
            foreach (ConstraintTypeCodes T in Enum.GetValues(typeof(ConstraintTypeCodes)).Cast<ConstraintTypeCodes>())
            {
                if (T == ConstraintTypeCodes.Unset)
                    continue;
                ConstraintTypes.Add(T);
            }
            SetComboBoxes();
            Components.Clear();
            foreach (var CN in Ctr.GetComponentViewList())
            {
                Components.Add(new ComponentSelector(CN));
            }
            CV.ConstraintEvaluating += OnConstraintEvaluating;
            CV.ConstraintEvaluated += OnConstraintEvaluated;
            CV.PropertyChanged += OnConstraintPropertyChanged;
            SSin.PropertyChanged += OnStructurePropertyChanged;
            RaisePropertyChangedEvent("ChangeStatusString");
            RaisePropertyChangedEvent(nameof(ConstraintValueColor));
            RaisePropertyChangedEvent(nameof(ReferenceValueColor));
        }
        // Event Handlers
        private void OnConstraintEvaluated(object sender, int AssessmentId)
        {
            RefreshFlag = !RefreshFlag;
        }
        private void OnConstraintEvaluating(object sender, int AssessmentId)
        {
            RefreshFlag = !RefreshFlag;
        }
        private void OnConstraintPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "DisplayOrder":
                    RaisePropertyChangedEvent("DisplayOrder");
                    break;
                case "ConstraintDefinition":
                    RefreshRowHeader = !RefreshRowHeader;
                    RaisePropertyChangedEvent("ChangeStatusString");
                    RaisePropertyChangedEvent("RefreshRowHeader");
                    RaisePropertyChangedEvent("ShortConstraintDefinition");
                    break;
                case "NumFractions":
                    RefreshRowHeader = !RefreshRowHeader;
                    RaisePropertyChangedEvent("ReferenceValue");
                    RaisePropertyChangedEvent("ConstraintValue");
                    RaisePropertyChangedEvent(nameof(ConstraintValueColor));
                    RaisePropertyChangedEvent(nameof(ReferenceValueColor));
                    break;
                case "Threshold":
                    RaisePropertyChangedEvent("ChangeStatusString");
                    RaisePropertyChangedEvent("StopValue");
                    RaisePropertyChangedEvent("MinorViolation");
                    RaisePropertyChangedEvent("MajorViolation");
                    break;
            }
        }
        private void OnStructurePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "EclipseStructureName":
                    RefreshRowHeader = !RefreshRowHeader;
                    //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RefreshRowHeader"));
                    RaisePropertyChangedEvent("RefreshRowHeader");

                    break;
            }
        }
        public ObservableCollection<ConstraintTypeCodes> ConstraintTypes { get; private set; } = new ObservableCollection<ConstraintTypeCodes>() { ConstraintTypeCodes.Unset };
        public ObservableCollection<ConstraintUnits> ConstraintUnitTypes { get; private set; } = new ObservableCollection<ConstraintUnits>() { ConstraintUnits.Unset };
        public ObservableCollection<ReferenceTypes> AvailableReferenceTypes { get; private set; } = new ObservableCollection<ReferenceTypes>() { ReferenceTypes.Unset };
        public ObservableCollection<ConstraintUnits> AvailableReferenceUnitTypes { get; private set; } = new ObservableCollection<ConstraintUnits>() { ConstraintUnits.Unset };
        public ObservableCollection<ComponentSelector> Components { get; private set; } = new ObservableCollection<ComponentSelector>() { };

    }
    [AddINotifyPropertyChangedInterface]
    public class AssignmentSelector
    {
        public int ComponentId { get; set; }
        public int PlanId { get; set; }
    }
    //[AddINotifyPropertyChangedInterface]
    //public class AssessmentPlanSelector
    //{
    //    public PatientView PV { get; set; }
    //    public ComponentSelector CS { get; set; }
    //    private CourseSelector _Course;
    //    public CourseSelector Course
    //    {
    //        get { return _Course; }
    //        set
    //        {
    //            if (value != _Course)
    //            {
    //                _Course = value;
    //                AvailablePlans.Clear();
    //                PlanTypes P = PlanTypes.Unset;
    //                switch (CS.ComponentType)
    //                {
    //                    case (ComponentTypes.Plan):
    //                        P = PlanTypes.Single;
    //                        break;
    //                    case (ComponentTypes.Sum):
    //                        P = PlanTypes.Sum;
    //                        break;

    //                }
    //                foreach (var A in PV.Courses.FirstOrDefault(x => x.CourseId == _Course.CourseId).Plans.Where(x => x.PlanType == P))
    //                {
    //                    AvailablePlans.Add(A);
    //                }
    //            }
    //        }
    //    }
    //    public ObservableCollection<PlanSelector> AvailablePlans = new ObservableCollection<PlanSelector>();
    //    public AssessmentPlanSelector(PatientView pv)
    //    {
    //        PV = pv;
    //    }
    //}
    [AddINotifyPropertyChangedInterface]
    public class PlanSelector
    {
        public string PlanId { get; set; } = "";
        public string CourseId { get; set; } = "";
        public PlanTypes PlanType { get; set; } = PlanTypes.Unset;
        public PlanSelector(string planId = "", string courseId = "", AssessmentComponentView ACVinit = null)
        {
            PlanId = planId;
            CourseId = courseId;
            ACV = ACVinit;
        }
        public AssessmentComponentView ACV = null;
        public Controls.Control_ViewModel VM = new Controls.Control_ViewModel();
    }
    [AddINotifyPropertyChangedInterface]
    public class CourseSelector
    {
        public string CourseId { get; set; }
        public CourseSelector(string courseId = "")
        {
            CourseId = courseId;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class StructureSetSelector 
    {
        public string StructureSetId { get; set; }
        public StructureSetSelector(string structureSetId = "")
        {
            StructureSetId = structureSetId;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class PatientView : ObservableObject
    {
        public string PatientId { get; set; } = "";
        public Presenter ParentView;
        public string FullPatientName { get; private set; }
        public System.Windows.Media.SolidColorBrush TextBox_Background_Color { get; set; } = new System.Windows.Media.SolidColorBrush(wpfcolors.AliceBlue);
        public ObservableCollection<CourseSelector> Courses { get; set; } = new ObservableCollection<CourseSelector>() { new CourseSelector() };
        public ObservableCollection<StructureSetSelector> StructureSets { get; set; } = new ObservableCollection<StructureSetSelector>() { new StructureSetSelector() };
        private StructureSetSelector _CurrentStructureSet;
        public StructureSetSelector CurrentStructureSet
        {
            get
            {
                return _CurrentStructureSet;
            }
            set
            {
                if (_CurrentStructureSet != value)
                {
                    _CurrentStructureSet = value;
                    // Apply structure aliasing
                    if (value !=null)
                        SetCurrentStructureSet(_CurrentStructureSet.StructureSetId);
                }
            }
        }

        private async void SetCurrentStructureSet(string structureSetId)
        {
            if (ParentView != null)
            {
                ParentView.isLoading = true;
                ParentView.LoadingString = "Applying structure aliases...";
                bool success = await Task.Run (() => Ctr.SetCurrentStructureSet(_CurrentStructureSet.StructureSetId));
                ParentView.isLoading = false;
            }
        }
    
        public PatientView(string patientId = "")
        {
            Ctr.PatientOpened += OnPatientOpened;
            Ctr.CurrentStructureSetChanged += OnCurrentStructureSetChanged;
        }
        private void OnPatientOpened(object sender, EventArgs e)
        {
            PatientId = Ctr.PatientID;
            FullPatientName = string.Format("{0}, {1}", Ctr.PatientLastName, Ctr.PatientFirstName);
            StructureSets.Clear();
            foreach (string SSid in Ctr.GetAvailableStructureSetIds())
            {
                StructureSets.Add(new StructureSetSelector(SSid));
            }
        }
        private void OnCurrentStructureSetChanged(object sender, EventArgs e)
        {
            _CurrentStructureSet = StructureSets.FirstOrDefault(x => x.StructureSetId == Ctr.CurrentStructureSet.Id);
            RaisePropertyChangedEvent(nameof(CurrentStructureSet));
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class AssessmentComponentView : ObservableObject
    {
        public string ComponentName
        {
            get { return sCompV.ComponentName; }
            set
            {
                if (value != sCompV.ComponentName)
                {
                    sCompV.ComponentName = value;
                }
            }
        }
        public bool Warning { get; set; } = false;
        public string WarningString { get; set; } = "";
        public bool EnableCourseSelection { get; set; } = true;
        public bool DisableAutomaticAssociation = false;
        private CourseSelector _SelectedCourse;
        public CourseSelector SelectedCourse
        {
            get { return _SelectedCourse; }
            set
            {
                RaisePropertyChangedEvent("EnableCourseSelection");
                if (_SelectedCourse != value && value != null) // refresh available plans
                {
                    _SelectedCourse = value;
                    EnableCourseSelection = false;
                    SetPlanIds(_SelectedCourse.CourseId);
                }

            }
        }
        private async void SetPlanIds(string CourseId)
        {
            try
            {
                ParentView.ParentView.WaitingForUpdate = true;
                ParentView.ParentView.WaitingDescription = "Loading plans...";
                List<string> result = await Ctr.GetPlanIdsByCourseName(_SelectedCourse.CourseId);
                ObservableCollection<PlanSelector> NewPlans = new ObservableCollection<PlanSelector>();
                foreach (string PlanId in result)
                {
                    NewPlans.Add(new PlanSelector(PlanId, CourseId, this));
                }
                Plans = NewPlans;
                ParentView.ParentView.WaitingForUpdate = false;
                EnableCourseSelection = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0} {1} {2}", ex.Message, ex.InnerException, ex.StackTrace));
            }
        }
        private PlanSelector _SelectedPlan;
        public PlanSelector SelectedPlan
        {
            get { return _SelectedPlan; }
            set
            {
                if (value == null)
                {
                    if (!DisableAutomaticAssociation)
                        Ctr.GetAssessmentView(sAV.ID).ClearComponentAssociation(sCompV.ID);
                }
                if (value != _SelectedPlan)
                {
                    _SelectedPlan = value;
                    if (value != null)
                        SetPlanAsync();
                    RaisePropertyChangedEvent("SelectedPlan");
                }
            }
        }
        private Ctr.ComponentView sCompV;
        private Ctr.AssessmentView sAV;
        public AssessmentView ParentView;
        public ObservableCollection<CourseSelector> Courses { get; set; } = new ObservableCollection<CourseSelector>();
        public ObservableCollection<PlanSelector> Plans { get; set; } = new ObservableCollection<PlanSelector>();
        public AssessmentComponentView(AssessmentView AV, Ctr.ComponentView scompv, Ctr.AssessmentView sav)
        {
            sCompV = scompv;
            sCompV.ComponentChanged += UpdateStatus;
            sAV = sav;
            ParentView = AV;
            var PV = Ctr.GetPlanView(sCompV.ID, sav.ID); // check if plan is associated
            if (PV != null)
                Warning = Ctr.GetPlanView(sCompV.ID, sav.ID).LoadWarning; // initialize warning 
            foreach (string CourseName in Ctr.GetCourseNames())
            {
                Courses.Add(new CourseSelector(CourseName));
            }

        }
        private async void SetPlanAsync()
        {
            if (!DisableAutomaticAssociation)
            {
                var CSC = await Task.Run(() => Ctr.GetAssessmentView(sAV.ID).AssociatePlanToComponent(sCompV.ID, _SelectedCourse.CourseId, _SelectedPlan.PlanId, true));
                UpdateWarning(CSC);
            }
            ParentView.UpdateWarning();
        }
        private void UpdateStatus(object sender, EventArgs e)
        {
            var CV = (sender as Ctr.ComponentView);
            var CSC = sAV.StatusCodes(sCompV.ID);
            UpdateWarning(CSC);
        }
        private void UpdateWarning(List<ComponentStatusCodes> CSC)
        {
            Warning = false;
            WarningString = "";
            foreach (var status in CSC)
            {
                if (status == ComponentStatusCodes.Evaluable)
                    continue;
                else
                {
                    Warning = true;
                    if (WarningString == "")
                        WarningString = status.Display();
                    else
                        WarningString = WarningString + System.Environment.NewLine + status.Display();
                }
            }
            ParentView.UpdateWarning();
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class ProtocolView : ObservableObject
    {
        public class ProtocolSelector : ObservableObject
        {
            public int ID { get; private set; }
            public string ProtocolName { get { return PV.ProtocolName; } }
            public ProtocolTypes ProtocolType { get { return PV.ProtocolType; } }
            public TreatmentCentres TreatmentCentre { get { return PV.TreatmentCentre; } }
            public TreatmentSites TreatmentSite { get { return PV.TreatmentSite; } }
            public ApprovalLevels ApprovalLevel { get { return PV.ApprovalLevel; } }
            public string LastModifiedBy { get { return PV.LastModifiedBy; } }
            private Ctr.ProtocolView PV;
            public ProtocolSelector(Ctr.ProtocolView PV_in)
            {
                PV = PV_in;
                ID = PV.ProtocolID;
                Ctr.ProtocolListUpdated += OnProtocolListUpdated;
            }
            private void OnProtocolListUpdated(object sender, EventArgs e)
            {
                PV = Ctr.GetProtocolList(ID);
            }
            public void Unsubscribe()
            {
                Ctr.ProtocolListUpdated -= OnProtocolListUpdated;
            }
        }
        public ProtocolView(string inputProtocolName = "No protocol loaded")
        {
            PV = Ctr.GetProtocolView();
            ProtocolName = PV.ProtocolName;
            RaisePropertyChangedEvent(nameof(LastModifiedBy));
            var ProtocolPreviews = Ctr.GetProtocolList();
            foreach (Ctr.ProtocolView P in ProtocolPreviews)
            {
                Protocols.Add(new ProtocolSelector(P));
            }
            // Subscribe to events
            Ctr.LinkedPlansChanged += UpdateAvailableStructureIds;
            Ctr.ProtocolListUpdated += UpdateProtocolList;
            Ctr.ProtocolConstraintOrderChanged += UpdateConstraintOrder;
            Ctr.ConstraintAdded += OnConstraintAdded;
            Ctr.ConstraintRemoved += OnConstraintRemoved;
            if (PV != null)
                PV.PropertyChanged += OnProtocolPropertyChanged;
        }
        public void Unsubscribe()
        {
            Ctr.LinkedPlansChanged -= UpdateAvailableStructureIds;
            Ctr.ProtocolListUpdated -= UpdateProtocolList;
            Ctr.ProtocolConstraintOrderChanged -= UpdateConstraintOrder;
            Ctr.ConstraintAdded -= OnConstraintAdded;
            Ctr.ConstraintRemoved -= OnConstraintRemoved;
            if (PV != null)
                PV.PropertyChanged -= OnProtocolPropertyChanged;
            foreach (ProtocolSelector PS in Protocols)
            {
                PS.Unsubscribe();
            }
        }
        private Ctr.ProtocolView PV;
        private string _ProtocolName = "No protocol loaded";
        public string ProtocolName
        {
            get { return _ProtocolName; }
            set
            {
                if (value != _ProtocolName)
                {
                    _ProtocolName = value;
                    Ctr.GetProtocolView().ProtocolName = value;
                    //RaisePropertyChangedEvent(nameof(Protocols));
                }
            }
        }
        public ProtocolTypes ProtocolType
        {
            get { return Ctr.GetProtocolView().ProtocolType; }
            set { Ctr.GetProtocolView().ProtocolType = value; }
        }
        public TreatmentCentres TreatmentCentre
        {
            get { return Ctr.GetProtocolView().TreatmentCentre; }
            set { Ctr.GetProtocolView().TreatmentCentre = value; }
        }
        public TreatmentSites TreatmentSite
        {
            get { return Ctr.GetProtocolView().TreatmentSite; }
            set { Ctr.GetProtocolView().TreatmentSite = value; }
        }
        public string LastModifiedBy
        {
            get
            {
                if (PV != null)
                    return PV.LastModifiedBy;
                else
                    return "";
            }
        }
        public bool RefreshFlag { get; private set; } = false;
        public bool DragSelected { get; set; } = false;
        private int _SelectedIndex = -2;
        public int SelectedIndex
        {
            get
            {
                return _SelectedIndex;
            }
            set
            {
                if (value == -1)
                {
                    string debgume = "hi";
                }
                _SelectedIndex = value;
            }
        }

        public static readonly DependencyProperty IsDraggingProperty = DependencyProperty.RegisterAttached("IsDragging", typeof(bool), typeof(ProtocolView), new UIPropertyMetadata(false));

        public static bool GetIsDragging(DependencyObject source)
        {
            return (bool)source.GetValue(IsDraggingProperty);
        }

        public static void SetIsDragging(DependencyObject target, bool value)
        {
            target.SetValue(IsDraggingProperty, value);
        }
        public int DropIndex { get; set; } = -1;
        public StructureSelector SelectedStructure { get; set; }
        public int _ProgressPercentage;
        public int ProgressPercentage
        {
            get { return _ProgressPercentage; }
            set
            {
                _ProgressPercentage = value;
                //RaisePropertyChangedEvent();
            }
        }
        public Progress<int> Progress { get; set; }
        public ObservableCollection<StructureSelector> Structures { get; set; } = new ObservableCollection<StructureSelector>();
        public ObservableCollection<Ctr.EclipseStructure> AvailableStructureIds { get; private set; } = new ObservableCollection<Ctr.EclipseStructure>() { new Ctr.EclipseStructure(null) };
        public ObservableCollection<ProtocolSelector> Protocols { get; set; } = new ObservableCollection<ProtocolSelector>();
        public ObservableCollection<ComponentSelector> Components { get; set; } = new ObservableCollection<ComponentSelector>();
        public ObservableCollection<ConstraintSelector> Constraints { get; set; } = new ObservableCollection<ConstraintSelector>();
        private ConstraintSelector _SelectedConstraint;
        public ConstraintSelector SelectedConstraint
        {
            get { return _SelectedConstraint; }
            set
            {
                if (value != _SelectedConstraint)
                {
                    _SelectedConstraint = value;
                }
            }
        }
        private void UpdateAvailableStructureIds(object sender, EventArgs e)
        {
            ObservableCollection<Ctr.EclipseStructure> StructureIds = new ObservableCollection<Ctr.EclipseStructure>();
            foreach (Ctr.EclipseStructure Structure in Ctr.GetAllLinkedStructures())
            {
                StructureIds.Add(Structure);
            }
            AvailableStructureIds = StructureIds;
        }
        private void UpdateProtocolList(object sender, EventArgs e)
        {
            ObservableCollection<ProtocolSelector> UpdatedList = new ObservableCollection<ProtocolSelector>();
            foreach (Ctr.ProtocolView P in Ctr.GetProtocolList())
            {
                UpdatedList.Add(new ProtocolSelector(P));
            }
            Protocols = UpdatedList;
        }
        private void UpdateConstraintOrder(object sender, EventArgs e)
        {
            //ObservableCollection<ConstraintSelector> CSReorder = new ObservableCollection<ConstraintSelector>();
            //foreach (ConstraintSelector CS in Constraints.OrderBy(x => x.DisplayOrder))
            //    CSReorder.Add(CS);
            //for (int c = 0; c < CSReorder.Count; c++) // only re-bind collection if necessary
            //{
            //    if (CSReorder[c] != Constraints[c])
            //    {
            //        Constraints = CSReorder;
            //        break;
            //    }
            //}
        }
        private void OnConstraintAdded(object sender, int Id)
        {
            Ctr.ConstraintView CV = Ctr.GetConstraintView(Id);
            if (CV != null)
            {
                StructureSelector SS = Structures.FirstOrDefault(x => x.Id == CV.PrimaryStructureID);
                ConstraintSelector CS = new ConstraintSelector(CV, SS);
                var test = Constraints.FirstOrDefault(x => x.DisplayOrder == (CV.DisplayOrder - 1));
                int Order = Constraints.IndexOf(Constraints.FirstOrDefault(x => x.DisplayOrder == (CV.DisplayOrder - 1)));
                Constraints.Insert(Order + 1, CS);
            }
        }
        private void OnConstraintRemoved(object sender, int Id)
        {
            Constraints.Remove(Constraints.FirstOrDefault(x => x.Id == Id));
        }
        private void OnProtocolPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Ctr.ProtocolView.LastModifiedBy):
                    RaisePropertyChangedEvent(nameof(LastModifiedBy));
                    break;
            }
            RaisePropertyChangedEvent(nameof(Protocols));
        }
        public void AddConstraint()
        {
            var CV = Ctr.AddConstraint(ConstraintTypeCodes.Unset, Components.FirstOrDefault().Id, Structures.FirstOrDefault().Id);
            Constraints.Add(new ConstraintSelector(CV, Structures.FirstOrDefault()));
        }
        public void AddStructure()
        {
            var SV = Ctr.AddNewStructure();
            Structures.Add(new StructureSelector(SV));
        }

    }
    [AddINotifyPropertyChangedInterface]
    public class AssessmentView : ObservableObject
    {
        public string ContentControlDataField { get; set; }
        public wpfcolor Color { get; set; }
        public wpfcolor TextColor { get; set; }
        public string TextDataField { get; set; }
        public string AssessmentName { get; set; }
        public bool Pinned { get; set; } = false;
        public bool Warning
        {
            get
            {
                foreach (var ACV in ACVs)
                {
                    if (ACV.Warning)
                        return true;
                }
                return false;
            }
        }
        public void UpdateWarning()
        {
            RaisePropertyChangedEvent("Warning"); // this to update the warning symbol in the parent view;
        }

        public int AssessmentId
        {
            get { return sav.ID; }
        }
        public AssessmentsView ParentView;
        private Ctr.AssessmentView sav;
        public int ComponentCount
        {
            get { return ACVs.Count; }
        }
        public ObservableCollection<AssessmentComponentView> ACVs { get; set; } = new ObservableCollection<AssessmentComponentView>();
        public AssessmentView(wpfcolor Color_in, wpfcolor TextColor_in, AssessmentsView ParentView_in)
        {
            ParentView = ParentView_in;
            Color = Color_in;
            TextColor = TextColor_in;
            if (!Ctr.PatientLoaded)
            {
                MessageBox.Show("Please load patient first...");
                return;
            }
            sav = Ctr.NewAssessment();
            AssessmentName = sav.AssessmentName;
            foreach (Ctr.ComponentView sCompV in Ctr.GetComponentViewList())
            {
                ACVs.Add(new AssessmentComponentView(this, sCompV, sav));
            }
        }
        public AssessmentView(Ctr.AssessmentView AV, wpfcolor Color_in, wpfcolor TextColor_in, AssessmentsView ParentView_in)
        {
            sav = AV;
            Color = Color_in;
            TextColor = TextColor_in;
            AssessmentName = AV.AssessmentName;
            ParentView = ParentView_in;
            foreach (Ctr.ComponentView sCompV in Ctr.GetComponentViewList())
            {
                var ACV = new AssessmentComponentView(this, sCompV, sav);
                ACV.DisableAutomaticAssociation = true;
                var PV = Ctr.GetPlanView(sCompV.ID, AV.ID);
                if (PV != null)
                {
                    ACV.WarningString = PV.LoadWarningString;
                    if (!PV.LoadWarning) // if there's a load warning (e.g can't find the file, don't set the combo boxes
                    {
                        ACV.SelectedCourse = ACV.Courses.FirstOrDefault(x => x.CourseId == PV.CourseName);
                        ACV.SelectedPlan = ACV.Plans.FirstOrDefault(x => x.PlanId == PV.PlanName);
                        foreach (ComponentStatusCodes code in PV.ErrorCodes)
                        {
                            if (code == ComponentStatusCodes.Evaluable)
                                continue;
                            else
                            {
                                ACV.Warning = true;
                                ACV.WarningString = string.Join(System.Environment.NewLine, code.Display());
                            }
                        }
                    }
                    else
                        ACV.Warning = true;
                }
                ACV.DisableAutomaticAssociation = false;
                ACVs.Add(ACV);
            }

        }
        public void Delete()
        {
            Ctr.RemoveAssessment(sav.ID);
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class AssessmentsView : ObservableObject
    {
        public Progress<int> Progress { get; set; }
        public Thickness ColHeaderMargin { get; set; } = new Thickness(10, 5, 10, 5);
        //public DataGridLength RowHeaderWidth { get; set; } = new DataGridLength(1, DataGridLengthUnitType.Auto);
        public double RowHeaderWidth { get; set; } = double.NaN;
        public int AssessmentCounter { get; private set; } = 1;
        public bool WaitingForUpdate { get; set; } = false;
        public string WaitingDescription { get; set; } = "";
        public double FontSize { get; set; } = 12;
        private List<wpfcolor> DefaultAssessmentColors = new List<wpfcolor> { wpfcolors.LightSteelBlue, wpfcolors.AliceBlue, wpfcolors.PapayaWhip, wpfcolors.PaleGoldenrod };
        private List<wpfcolor> DefaultAssessmentTextColors = new List<wpfcolor> { wpfcolors.White, wpfcolors.Black, wpfcolors.Black, wpfcolors.Black };

        public AssessmentView SelectedAssessment { get; set; }
        public ObservableCollection<AssessmentView> Assessments { get; set; } = new ObservableCollection<AssessmentView>();
        public ObservableCollection<SquintDataColumn> AssessmentColumns { get; set; } = new ObservableCollection<SquintDataColumn>();
        public void AddAssessment()
        {
            if (Ctr.PatientLoaded && Ctr.ProtocolLoaded)
            {
                int colindex = (AssessmentCounter - 1) % DefaultAssessmentColors.Count;
                AssessmentView AV = new AssessmentView(DefaultAssessmentColors[colindex], DefaultAssessmentTextColors[colindex], this);
                Assessments.Add(AV);
                //Add new column
                SquintDataColumn dgtc = new SquintDataColumn(AV)
                {
                    //HeaderTemplate = (DataTemplate)Resources["myColumnHeaderTemplate"],
                    HeaderStyle = (Style)Application.Current.FindResource("SquintColumnHeaderStyle"),
                    CellTemplate = (DataTemplate)Application.Current.FindResource("SquintCellTemplate"),
                    CellStyle = (Style)Application.Current.FindResource("SquintCellStyle"),
                    Header = AV.AssessmentName,
                    Width = DataGridLength.Auto,
                };
                AssessmentColumns.Add(dgtc);
                AssessmentCounter++;
            }
            else
            {
                MessageBox.Show("Please load patient and protocol first", "No open Protocol/Patient");
                return;
            }

        }
        public void LoadAssessmentViews()
        {
            if (Ctr.PatientLoaded && Ctr.ProtocolLoaded)
            {
                foreach (Ctr.AssessmentView SAV in Ctr.GetAssessmentViewList())
                {
                    int colindex = (AssessmentCounter - 1) % DefaultAssessmentColors.Count;
                    AssessmentView AV = new AssessmentView(SAV, DefaultAssessmentColors[colindex], DefaultAssessmentTextColors[colindex], this);
                    Assessments.Add(AV);
                    //Add new column
                    SquintDataColumn dgtc = new SquintDataColumn(AV)
                    {
                        //HeaderTemplate = (DataTemplate)Resources["myColumnHeaderTemplate"],
                        HeaderStyle = (Style)Application.Current.FindResource("SquintColumnHeaderStyle"),
                        CellTemplate = (DataTemplate)Application.Current.FindResource("SquintCellTemplate"),
                        CellStyle = (Style)Application.Current.FindResource("SquintCellStyle"),
                        Header = AV.AssessmentName,
                        Width = DataGridLength.Auto,
                    };
                    AssessmentColumns.Add(dgtc);
                    AssessmentCounter++;
                }
            }

            else
            {
                MessageBox.Show("Please load patient and protocol first", "No open Protocol/Patient");
                return;
            }
        }
        public void DeleteAssessment(AssessmentView AS)
        {
            var UpdatedAssessments = new ObservableCollection<AssessmentView>();
            foreach (AssessmentView AV in Assessments)
            {
                if (AV != AS)
                    UpdatedAssessments.Add(AV);
            }
            Assessments = UpdatedAssessments;
            AssessmentColumns.Remove(AssessmentColumns.FirstOrDefault(x => x.Header.ToString() == AS.AssessmentName));
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class ConstraintResultView
    {
        private Ctr.ConstraintResultView sCRV;
        public string Result
        {
            get { return sCRV.Result; }
        }
        public ConstraintResultView(Ctr.ConstraintResultView crv)
        {
            sCRV = crv;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class ConstraintResultsView
    {
        public Ctr.ConstraintView CV;
        private int _UpdatedId { get; set; }
        public int UpdatedId
        {
            get { return _UpdatedId; }
            set { _UpdatedId = value; }
        }
        public Dictionary<int, ConstraintResultView> ConstraintResults;
        public ConstraintResultsView(Ctr.ConstraintView cv)
        {
            CV = cv;
            CV.ConstraintEvaluated += OnConstraintEvaluated;
        }
        private void OnConstraintEvaluated(object sender, int AssessmentId)
        {
            _UpdatedId = AssessmentId;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class SessionsView
    {
        public ObservableCollection<Ctr.SessionView> SessionViews { get; private set; } = new ObservableCollection<Ctr.SessionView>();
        public string SessionComment { get; set; }
        public SessionsView()
        {
            Ctr.SessionsChanged -= OnSessionsChanged;
            Ctr.SessionsChanged += OnSessionsChanged;
            foreach (Ctr.SessionView SV in Ctr.GetSessionViews())
                SessionViews.Add(SV);
        }
        public void OnSessionsChanged(object sender, EventArgs e)
        {
            ObservableCollection<Ctr.SessionView> updatedSV = new ObservableCollection<Ctr.SessionView>();
            foreach (Ctr.SessionView SV in Ctr.GetSessionViews())
                updatedSV.Add(SV);
            SessionViews = updatedSV;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class Presenter
    {
        [AddINotifyPropertyChangedInterface]
        public class FilterComboBox : ObservableObject
        {
            public FilterComboBox(ViewEnums FilterType)
            {
                _ComboBoxHeader = FilterType.Display();
                View = FilterType;
                var NewComboSourceList = new ObservableCollection<object>();
                Ctr.ProtocolOpened += OnProtocolOpened;
                switch (FilterType)
                {
                    case ViewEnums.Centre:
                        foreach (TreatmentCentres TC in Enum.GetValues(typeof(TreatmentCentres)))
                        {
                            if (TC == TreatmentCentres.Unset || TC == TreatmentCentres.All)
                                continue;
                            else
                                NewComboSourceList.Add(TC);
                        }
                        ComboSourceList = NewComboSourceList;
                        break;
                    case ViewEnums.Site:
                        foreach (TreatmentSites TC in Enum.GetValues(typeof(TreatmentSites)))
                        {
                            if (TC == TreatmentSites.Unset || TC == TreatmentSites.All)
                                continue;
                            else
                                NewComboSourceList.Add(TC);
                        }
                        ComboSourceList = NewComboSourceList;
                        break;
                    case ViewEnums.ProtocolType:
                        foreach (ProtocolTypes TC in Enum.GetValues(typeof(ProtocolTypes)))
                        {
                            if (TC == ProtocolTypes.Unset || TC == ProtocolTypes.All)
                                continue;
                            else
                                NewComboSourceList.Add(TC);
                        }
                        ComboSourceList = NewComboSourceList;
                        break;
                    case ViewEnums.Approval:
                        foreach (ApprovalLevels TC in Enum.GetValues(typeof(ApprovalLevels)))
                        {
                            if (TC == ApprovalLevels.Unset || TC == ApprovalLevels.All)
                                continue;
                            else
                                NewComboSourceList.Add(TC);
                        }
                        ComboSourceList = NewComboSourceList;
                        break;
                }
            }
            public ViewEnums View { get; private set; }
            private string _ComboBoxHeader = "Default";
            public string ComboBoxHeader
            {
                get
                {
                    return _ComboBoxHeader;
                }
                set
                {
                    _ComboBoxHeader = value;
                    RaisePropertyChangedEvent("ComboBoxHeader");
                }
            }
            public ObservableCollection<object> ComboSourceList { get; set; } = new ObservableCollection<object>() { "Test1", "Test2" };
            public object ComboSelectedItem
            {
                get
                {
                    switch (View)
                    {
                        case ViewEnums.Centre:
                            if (Ctr.ProtocolLoaded)
                                return Ctr.GetProtocolView().TreatmentCentre;
                            else
                                return null;
                        case ViewEnums.Site:
                            if (Ctr.ProtocolLoaded)
                                return Ctr.GetProtocolView().TreatmentSite;
                            else
                                return null;
                        case ViewEnums.ProtocolType:
                            if (Ctr.ProtocolLoaded)
                                return Ctr.GetProtocolView().ProtocolType;
                            else
                                return null;
                        case ViewEnums.Approval:
                            if (Ctr.ProtocolLoaded)
                                return Ctr.GetProtocolView().ApprovalLevel;
                            else
                                return null;
                        default:
                            return null;
                    }
                }
                set
                {
                    switch (View)
                    {
                        case ViewEnums.Centre:
                            if (Ctr.ProtocolLoaded)
                                Ctr.GetProtocolView().TreatmentCentre = (TreatmentCentres)value;
                            break;
                        case ViewEnums.Site:
                            if (Ctr.ProtocolLoaded)
                                Ctr.GetProtocolView().TreatmentSite = (TreatmentSites)value;
                            break;
                        case ViewEnums.ProtocolType:
                            if (Ctr.ProtocolLoaded)
                                Ctr.GetProtocolView().ProtocolType = (ProtocolTypes)value;
                            break;
                        case ViewEnums.Approval:
                            if (Ctr.ProtocolLoaded)
                                Ctr.GetProtocolView().ApprovalLevel = (ApprovalLevels)value;
                            break;
                    }
                }
            }
            private void OnProtocolOpened(object sender, EventArgs e)
            {
                RaisePropertyChangedEvent("ComboSelectedItem");
            }
        }
        private bool isSquintInitialized { get; set; } = false;
        public Controls.Beam_ViewModel Beam_ViewModel { get; set; } = new Controls.Beam_ViewModel();
        public Controls.LoadingViewModel Loading_ViewModel { get; set; } = new Controls.LoadingViewModel();
        public Controls.Control_ViewModel Objectives_ViewModel { get; set; } = new Controls.Control_ViewModel();
        public Controls.Imaging_ViewModel Imaging_ViewModel { get; set; } = new Controls.Imaging_ViewModel();
        public Controls.TestList_ViewModel Simulation_ViewModel { get; set; } = new Controls.TestList_ViewModel();
        public Controls.TestList_ViewModel Targets_ViewModel { get; set; } = new Controls.TestList_ViewModel();
        public Controls.TestList_ViewModel Calculation_ViewModel { get; set; } = new Controls.TestList_ViewModel();
        public Controls.TestList_ViewModel Prescription_ViewModel { get; set; } = new Controls.TestList_ViewModel();
        public ProtocolView Protocol { get; set; } = new ProtocolView();
        public PatientView PatientPresenter { get; set; } = new PatientView(null);
        public AssessmentsView AssessmentPresenter { get; set; } = new AssessmentsView();
        public SessionsView SessionsPresenter { get; set; } = new SessionsView();
        public ObservableCollection<ConstraintResultsView> SquintResults { get; set; } = new ObservableCollection<ConstraintResultsView>();
        public FilterComboBox CentreComboBox { get; set; } = new FilterComboBox(ViewEnums.Centre);
        public FilterComboBox SiteComboBox { get; set; } = new FilterComboBox(ViewEnums.Site);
        public FilterComboBox TypeComboBox { get; set; } = new FilterComboBox(ViewEnums.ProtocolType);
        public FilterComboBox ApprovalComboBox { get; set; } = new FilterComboBox(ViewEnums.Approval);
        public FilterComboBox ApproverComboBox { get; set; } = new FilterComboBox(ViewEnums.Approver);
        public AssessmentView NewAssessmentId { get; private set; }
        public bool isPIDVisible { get; set; } = false;
        public bool PlanCheckVisible { get; set; } = false;
        public string PlanCheckLoadingMessage { get; set; } = "Checking plan, please wait...";
        public bool isPlanCheckCalculating { get; set; } = false;
        public bool ProtocolCheckVisible { get; set; } = true;
        public bool SessionSelectVisibility { get; set; } = false;
        public bool SessionSaveVisibility { get; set; } = false;
        public bool ConstraintInfoVisibility { get; set; } = false;
        public bool isLoading { get; set; } = false;
        public string LoadingString { get; set; } = "";
        public bool isLinkProtocolVisible { get; set; } = false;
        public bool isUserPanelVisible { get; set; } = false;
        public bool isConfigVisible { get; set; } = false;
        public bool isLoadProtocolPanelVisible { get; set; } = true;
        public bool isStructurePanelVisible { get; set; } = true;
        public bool isProtocolLoaded { get; set; } = false;
        public bool isProtocolLoading { get; set; } = false;
        public ObservableCollection<string> Protocols { get; } = new ObservableCollection<string> { "Protocol1", "Protocol2" };

        //UI Commands
        public ICommand ChangeVisibilityCommand
        {
            get { return new DelegateCommand(ChangeVisibility); }
        }
        public ICommand SyncrhonizePatientCommand
        {
            get { return new DelegateCommand(SynchronizePatient); }
        }
        public ICommand ExpandLoadProtocolCommand
        {
            get { return new DelegateCommand(ExpandLoadProtocol); }
        }
        public ICommand ExpandStructuresCommand
        {
            get { return new DelegateCommand(ExpandStructures); }
        }
        public ICommand ChangeLinkVisibilityCommand
        {
            get { return new DelegateCommand(ChangeLinkProtocolVisibility); }
        }
        public ICommand AddAssessmentCommand
        {
            get { return new DelegateCommand(AddAssessment); }
        }
        public ICommand FontSizeIncreaseCommand
        {
            get { return new DelegateCommand(FontSizeIncrease); }
        }
        public ICommand FontSizeDecreaseCommand
        {
            get { return new DelegateCommand(FontSizeDecrease); }
        }
        public ICommand LoadSelectedProtocolCommand
        {
            get { return new DelegateCommand(LoadSelectedProtocol); }
        }
        public ICommand EnterKeyCommand_PID
        {
            get { return new DelegateCommand(LoadPatient); }
        }
        public ICommand ShowComponentCommand
        {
            get { return new DelegateCommand(ShowComponent); }
        }
        public ICommand AddConstraintCommand
        {
            get { return new DelegateCommand(AddConstraint); }
        }
        public ICommand AddStructureCommand
        {
            get { return new DelegateCommand(AddStructure); }
        }
        public ICommand ShowConfigCommand
        {
            get { return new DelegateCommand(ShowConfig); }
        }
        public ICommand ShiftConstraintUpCommand
        {
            get { return new DelegateCommand(ShiftConstraintUp); }
        }
        public ICommand ShiftConstraintDownCommand
        {
            get { return new DelegateCommand(ShiftConstraintDown); }
        }
        public ICommand AddConstraintBelowCommand
        {
            get { return new DelegateCommand(AddConstraintBelow); }
        }
        public ICommand DeleteConstraintCommand
        {
            get { return new DelegateCommand(DeleteConstraint); }
        }
        public ICommand ExpandUserPanelCommand
        {
            get { return new DelegateCommand(ExpandUserPanel); }
        }
        public ICommand LaunchAdminViewCommand
        {
            get { return new DelegateCommand(LaunchAdminView); }
        }
        public ICommand SaveWorkspaceCommand
        {
            get
            {
                return new DelegateCommand(SaveSessionDialog);
            }
        }
        public ICommand LoadWorkspaceCommand
        {
            get
            {
                return new DelegateCommand(LoadSession);
            }
        }
        public ICommand SaveSessionCommand
        {
            get
            {
                return new DelegateCommand(SaveSession);
            }
        }
        public ICommand LoadSelectedSessionCommand
        {
            get
            {
                return new DelegateCommand(LoadSelectedSession);
            }
        }
        public ICommand DeleteSelectedSessionCommand
        {
            get
            {
                return new DelegateCommand(DeleteSelectedSession);
            }
        }
        public ICommand ViewOptimizationObjectivesCommand
        {
            get
            {
                return new DelegateCommand(ViewOptimizationObjectives);
            }
        }
        public ICommand CloseCheckListCommand
        {
            get { return new DelegateCommand(CloseCheckList); }
        }
        private void CloseCheckList(object param = null)
        {
            ProtocolCheckVisible = true;
            PlanCheckVisible = false;
        }
        private async void ViewOptimizationObjectives(object param = null)
        {
            var p = (param as PlanSelector);
            if (param == null)
                return;
            else
            {
                ProtocolCheckVisible = false;
                PlanCheckVisible = true;
                isPlanCheckCalculating = true;
                Loading_ViewModel = new Controls.LoadingViewModel() { LoadingMessage = @"Checking plan, please wait..." };

                Objectives_ViewModel = new Controls.Control_ViewModel();
                var Objectives = await Ctr.GetOptimizationObjectiveList(p.CourseId, p.PlanId);
                List<string> StructureIds = new List<string>();
                ObservableCollection<Controls.ObjectiveItem> objectiveItems = new ObservableCollection<Controls.ObjectiveItem>();
                foreach (Controls.ObjectiveDefinition OD in Objectives)
                {
                    if (!StructureIds.Contains(OD.StructureId))
                    {
                        objectiveItems.Add(new Controls.ObjectiveItem(OD.StructureId, OD));
                        StructureIds.Add(OD.StructureId);
                    }
                    else
                        objectiveItems.Where(x => x.StructureId == OD.StructureId).FirstOrDefault().ObjectiveDefinitions.Add(OD);

                }
                Objectives_ViewModel.Objectives = objectiveItems;
                Objectives_ViewModel.NTO = await Ctr.GetNTOObjective(p.CourseId, p.PlanId);
                var ImagingFields = await Ctr.GetImagingFieldList(p.CourseId, p.PlanId);
                Ctr.ComponentView CV = Ctr.GetComponentViewList().FirstOrDefault(x => x.ComponentName == p.ACV.ComponentName);
                var ImageProtocolCheck = Ctr.CheckImagingProtocols(CV, ImagingFields);
                Imaging_ViewModel.ImagingProtocols.Clear();
                foreach (ImagingProtocols IP in CV.ImagingProtocolsAttached)
                {
                    Controls.ProtocolImagingView PIV = new Controls.ProtocolImagingView() { ImagingProtocolName = IP.Display() };
                    if (ImageProtocolCheck.ContainsKey(IP))
                    {
                        if (ImageProtocolCheck[IP].Count > 0)
                        {
                            PIV.WarningMessages = ImageProtocolCheck[IP];
                            PIV.isWarning = true;
                        }
                    }
                    Imaging_ViewModel.ImagingProtocols.Add(PIV);
                }
                Imaging_ViewModel.ImagingFields = new ObservableCollection<Ctr.ImagingFieldItem>(ImagingFields);
                // Populate Simulation ViewModel
                var SliceSpacingReference = CV.Checklist.SliceSpacing;
                if (SliceSpacingReference == 0)
                    SliceSpacingReference = Ctr.GetProtocolView().SliceSpacing;
                var SliceSpacingValue = await Ctr.GetSliceSpacing(p.CourseId, p.PlanId);
                var SliceSpacingWarning = false;
                var SliceSpacingWarningMessage = "";
                if (Math.Abs(SliceSpacingReference - SliceSpacingValue) > 0.1)
                {
                    SliceSpacingWarning = true;
                    SliceSpacingWarningMessage = "Slice spacing does not match protocol";
                }
                Controls.TestListItem SliceSpacing = new Controls.TestListItem("Slice spacing", string.Format("{0:0.##}", SliceSpacingValue), string.Format("{0:0.##}", SliceSpacingReference), SliceSpacingWarning, SliceSpacingWarningMessage);
                Controls.TestListItem Series = new Controls.TestListItem("Series Id", await Ctr.GetSeriesId(p.CourseId, p.PlanId));
                Controls.TestListItem Study = new Controls.TestListItem("Study Id", await Ctr.GetStudyId(p.CourseId, p.PlanId));
                Controls.TestListItem SeriesComment = new Controls.TestListItem("Series comment / scan protocol", await Ctr.GetSeriesComments(p.CourseId, p.PlanId));
                Controls.TestListItem ImageComment = new Controls.TestListItem("Image comment", await Ctr.GetImageComments(p.CourseId, p.PlanId));
                Controls.TestListItem NumSlices = new Controls.TestListItem("Number of slices", (await Ctr.GetNumSlices(p.CourseId, p.PlanId)).ToString());
                Simulation_ViewModel.Tests = new ObservableCollection<Controls.TestListItem>() { Study, Series, NumSlices, SliceSpacing, SeriesComment, ImageComment };
                // Populate Calculation ViewModel
                Calculation_ViewModel.Tests.Clear(); // = new ObservableCollection<Controls.TestListItem>();
                var ProtocolAlgorithm = CV.Checklist.Algorithm.Display();
                var ComponentAlgorithm = await Ctr.GetAlgorithmModel(p.CourseId, p.PlanId);
                var AlgorithmWarning = false;
                var AlgorithmWarningString = "Algorithm mismatch";
                if (ProtocolAlgorithm != ComponentAlgorithm)
                    AlgorithmWarning = true;
                Controls.TestListItem Algorithm = new Controls.TestListItem("Algorithm", ComponentAlgorithm, ProtocolAlgorithm, AlgorithmWarning, AlgorithmWarningString);
                Calculation_ViewModel.Tests.Add(Algorithm);
                var DGR_protocol = CV.Checklist.AlgorithmResolution;
                var DGRwarning = false;
                var DGRwarningMessage = "Insufficient resolution";
                var DGR_plan = await Ctr.GetDoseGridResolution(p.CourseId, p.PlanId);
                if (DGR_protocol < (DGR_plan - 0.01))
                    DGRwarning = true;
                Controls.TestListItem DoseGridResolution = new Controls.TestListItem("Dose grid resolution", DGR_plan.ToString(), DGR_protocol.ToString(), DGRwarning, DGRwarningMessage);
                Calculation_ViewModel.Tests.Add(DoseGridResolution);
                var HeteroOn = await Ctr.GetHeterogeneityOn(p.CourseId, p.PlanId);
                var ProtocolHeteroOn = CV.Checklist.HeterogeneityOn;
                var HeteroWarning = false;
                var HeteroWarningString = "Heterogeneity setting incorrect";
                if (HeteroOn != ProtocolHeteroOn)
                {
                    HeteroWarning = true;
                }
                Controls.TestListItem HeterogeneityOn = new Controls.TestListItem("Heterogeneity On", HeteroOn.ToString(), ProtocolHeteroOn.ToString(), HeteroWarning, HeteroWarningString);
                Calculation_ViewModel.Tests.Add(HeterogeneityOn); ;
                // Field Normalization
                var FieldNorm = await Ctr.GetFieldNormalizationMode(p.CourseId, p.PlanId);
                var ProtocolFieldNorm = CV.Checklist.FieldNormalizationMode.Display();
                var FieldNormWarning = false;
                if (FieldNorm != ProtocolFieldNorm)
                    FieldNormWarning = true;
                Controls.TestListItem FieldNormTest = new Controls.TestListItem("Field Norm Mode", FieldNorm, ProtocolFieldNorm, FieldNormWarning, "Non-standard normalization");
                Calculation_ViewModel.Tests.Add(FieldNormTest);
                // Support structures
                var CheckCouchSurface = await Ctr.GetCouchSurface(p.CourseId, p.PlanId);
                var CheckCouchInterior = await Ctr.GetCouchInterior(p.CourseId, p.PlanId);
                var RefCouchSurface = CV.Checklist.CouchSurface;
                var RefCouchInterior = CV.Checklist.CouchInterior;
                var CouchSurfaceWarning = false;
                var CouchInteriorWarning = false;
                var CouchSurfaceWarningMessage = "";
                var CouchInteriorWarningMessage = "";
                var CheckCouchSurfaceString = string.Format("{0:0.#}", CheckCouchSurface);
                var CheckCouchInteriorString = string.Format("{0:0.#}", CheckCouchInterior);
                var RefCouchSurfaceString = RefCouchSurface.ToString();
                var RefCouchInteriorString = RefCouchInterior.ToString();
                bool CouchFound = true;
                if (Double.IsNaN(CheckCouchSurface))
                {
                    CheckCouchSurfaceString = "Not found";
                    CouchFound = false;
                }
                else
                {
                    if (Math.Abs(CheckCouchSurface - RefCouchSurface) > 0.5)
                    {
                        CouchSurfaceWarning = true;
                        CouchSurfaceWarningMessage = "HU Deviation";
                        CheckCouchSurfaceString = string.Format("{0:0.#} HU", RefCouchSurface);
                    }
                    if (Math.Abs(CheckCouchInterior - RefCouchInterior) > 0.5)
                    {
                        CouchInteriorWarning = true;
                        CouchInteriorWarningMessage = "HU Deviation";
                        CheckCouchInteriorString = string.Format("{0:0.#} HU", RefCouchInterior);
                    }
                }
                switch (CV.Checklist.SupportIndication)
                {
                    case ParameterOptions.Optional:
                        if (!CouchFound)
                        {
                            CouchSurfaceWarning = false;
                            RefCouchSurfaceString = "No reference set";
                            RefCouchInteriorString = "No reference set";
                            CheckCouchSurfaceString = "Not found";
                            CheckCouchInteriorString = "Not found";
                        }
                        break;
                    case ParameterOptions.Required:
                        if (!CouchFound)
                        {
                            CouchSurfaceWarning = true;
                            CouchSurfaceWarningMessage = "Couch required";
                            CheckCouchInteriorString = "Not found";
                            CheckCouchSurfaceString = "Not found";
                        }
                        break;
                    case ParameterOptions.None:
                        if (CouchFound)
                        {
                            CouchSurfaceWarning = true;
                            CouchSurfaceWarningMessage = "No couch in protocol";
                        }
                        break;
                    default:
                        CouchSurfaceWarning = false;
                        RefCouchSurfaceString = "No reference set";
                        RefCouchInteriorString = "No reference set";
                        break;
                }
                Controls.TestListItem CouchSurfaceTest = new Controls.TestListItem("Couch Surface HU", CheckCouchSurfaceString, RefCouchSurfaceString, CouchSurfaceWarning, CouchSurfaceWarningMessage);
                Controls.TestListItem CouchInteriorTest = new Controls.TestListItem("Couch Interior HU", CheckCouchInteriorString, RefCouchInteriorString, CouchInteriorWarning, CouchInteriorWarningMessage);
                Calculation_ViewModel.Tests.Add(CouchSurfaceTest);
                Calculation_ViewModel.Tests.Add(CouchInteriorTest);
                // Artifacts in calculaion
                foreach (var A in CV.Checklist.Artifacts)
                {
                    var ArtifactWarning = false;
                    var ArtifactWarningString = "";
                    var RefHUString = string.Format("{0:0.#} \u00B1{1:0.#} HU", A.RefHU, A.ToleranceHU);
                    var CheckHUString = string.Format("{0:0.#} HU", A.CheckHU);
                    if (Double.IsNaN(A.CheckHU))
                        CheckHUString = "No artifact structure";
                    else
                    {
                        if (Math.Abs(A.RefHU - A.CheckHU) > A.ToleranceHU)
                        {
                            ArtifactWarning = true;
                            ArtifactWarningString = "Assigned HU deviates from protocol";
                        }
                    }
                    Calculation_ViewModel.Tests.Add(new Controls.TestListItem(string.Format(@"Artifact HU (""{0}"")", A.E.EclipseStructureName), CheckHUString, RefHUString, ArtifactWarning, ArtifactWarningString));
                }

                // Populate Prescription ViewModel
                var RefCourseIntent = Ctr.GetProtocolView().TreatmentIntent.Display();
                var CheckCourseIntent = await Ctr.GetCourseIntent(p.CourseId, p.PlanId);
                var CourseIntentWarning = false;
                var CourseIntentWarningString = "";
                if (RefCourseIntent.ToUpper() != CheckCourseIntent.ToUpper())
                {
                    CourseIntentWarning = true;
                }
                Controls.TestListItem CourseIntentTest = new Controls.TestListItem("Course Intent", CheckCourseIntent, RefCourseIntent, CourseIntentWarning, CourseIntentWarningString);
                var ProtocolPNVMax = CV.Checklist.PNVMax;
                var ProtocolPNVMin = CV.Checklist.PNVMin;
                var Acceptable_PNVRange = string.Format("{0:0.##} - {1:0.##}", ProtocolPNVMin, ProtocolPNVMax);
                var PlanPNV = await Ctr.GetPNV(p.CourseId, p.PlanId);
                var PNVWarning = false;
                if (PlanPNV < ProtocolPNVMin || PlanPNV > ProtocolPNVMax)
                {
                    PNVWarning = true;
                }
                var PlanRxPercentage = await Ctr.GetPrescribedPercentage(p.CourseId, p.PlanId);
                var PlanRxPercentageWarning = false;
                if (Math.Abs(PlanRxPercentage - 100) > 0.1)
                    PlanRxPercentageWarning = true;
                // Check Rx and fractions
                var CheckRxDose = Ctr.GetRxDose(p.CourseId, p.PlanId);
                var CheckRxDoseString = "-";
                var RxDoseWarningString = "";
                bool RxDoseWarning = false;
                if (CheckRxDose != null)
                {
                    CheckRxDoseString = string.Format("{0:0.##}", CheckRxDose);
                    if (Math.Abs((double)CheckRxDose - CV.ReferenceDose) > 1E-2)
                    {
                        RxDoseWarning = true;
                        RxDoseWarningString = "Plan dose different from protocol";
                    }
                }
                else
                {
                    RxDoseWarning = true;
                    RxDoseWarningString = "No total dose specified";
                }
                var CheckFractions = Ctr.GetNumFractions(p.CourseId, p.PlanId);
                var RefFractions = CV.NumFractions;
                var CheckFractionString = "-";
                var CheckFractionWarningString = "";
                var CheckFractionWarning = false;
                if (CheckFractions != null)
                {
                    CheckFractionString = CheckFractions.ToString();
                    if (CheckFractions != RefFractions)
                    {
                        CheckFractionWarning = true;
                        CheckFractionWarningString = "Plan fractions different from protocol";
                    }
                }
                else
                {
                    CheckFractionWarning = true;
                    CheckFractionWarningString = "No fractions specified";
                }
                Controls.TestListItem RxCheck = new Controls.TestListItem("Prescription dose", CheckRxDoseString, CV.ReferenceDose.ToString(), RxDoseWarning, RxDoseWarningString);
                Controls.TestListItem FxCheck = new Controls.TestListItem("Number of fractions", CheckFractionString, CV.NumFractions.ToString(), CheckFractionWarning, CheckFractionWarningString);
                Controls.TestListItem PNVCheck = new Controls.TestListItem("Plan Normalization Value Range", PlanPNV.ToString(), Acceptable_PNVRange, PNVWarning, "Out of range");
                Controls.TestListItem PlanRxPc = new Controls.TestListItem("Prescribed percentage", PlanRxPercentage.ToString(), "100", PlanRxPercentageWarning, "Not set to 100");
                Prescription_ViewModel.Tests = new ObservableCollection<Controls.TestListItem>() { RxCheck, FxCheck, CourseIntentTest, PNVCheck, PlanRxPc };
                // Beam checks
                Beam_ViewModel.Beams.Clear();
                Beam_ViewModel.GroupTests.Tests.Clear();
                var Fields = await Ctr.GetTxFieldItems(p.CourseId, p.PlanId);
                foreach (var Beam in CV.GetBeams())
                {
                    var BLI = new Controls.BeamListItem(Beam, Fields);
                    Beam_ViewModel.Beams.Add(BLI);
                    BLI.FieldChanged += new EventHandler((s, e) => BLI_PropertyChanged(s, e, CV)); // this updates the MinColOffsetCheck if the field assignments on any reference beam are changed
                }
                // Iso Check
                var IsoCentreWarning = Fields.Select(x => x.Isocentre).Distinct().Count() != CV.NumIsocentres;
                var NumIsoDetected = Fields.Select(x => x.Isocentre).Distinct().Count();
                Controls.TestListItem NumIsoCheck = new Controls.TestListItem("Number of isocentres", string.Format("{0:0}", NumIsoDetected), string.Format("{0:0}", CV.NumIsocentres), IsoCentreWarning, "Additional isocentres");
                Beam_ViewModel.GroupTests.Tests.Add(NumIsoCheck);
                // Num Fields Check
                var NumFieldsWarning = Fields.Count() > CV.MaxBeams || Fields.Count() < CV.MinBeams;
                string NumFieldsWarningString = "";
                if (NumFieldsWarning)
                    NumFieldsWarningString = "Out of range";
                if (CV.MinBeams > -1)
                {
                    Controls.TestListItem FieldCountCheck = new Controls.TestListItem("Number of fields", string.Format("{0:0}", Fields.Count()), string.Format("{0:0} - {1:0}", CV.MinBeams, CV.MaxBeams),
                    IsoCentreWarning, NumFieldsWarningString);
                    Beam_ViewModel.GroupTests.Tests.Add(FieldCountCheck);
                }
                // Min Col Offset
                Controls.TestListItem MinColOffsetCheck;
                if (CV.MaxBeams > 1 && !double.IsNaN(CV.MinColOffset) && Fields.Count > 1)
                {
                    if (Beam_ViewModel.Beams.Any(x => x.Field == null))
                    {
                        MinColOffsetCheck = new Controls.TestListItem("Min collimator offset", "", string.Format("{0:0.#}", CV.MinColOffset), true, "Protocol fields not assigned");
                    }
                    else
                    {
                        var ColOffset = Beam_ViewModel.Beams.Select(x => x.Field).Select(x => x.CollimatorAngle);
                        var MinColOffset = findMinDiff(ColOffset.ToArray());
                        var MinColOFfsetWarning = MinColOffset < CV.MinColOffset;
                        MinColOffsetCheck = new Controls.TestListItem("Min collimator offset", string.Format("{0:0.#}", MinColOffset), string.Format("{0:0.#}", CV.MinColOffset),
                            MinColOFfsetWarning, "");
                    }
                    Beam_ViewModel.GroupTests.Tests.Add(MinColOffsetCheck);
                }
                // Target Structure Checks
                Targets_ViewModel.Tests.Clear();
                foreach (Ctr.StructureView SV in Ctr.GetStructureViewList())
                {
                    if (SV.Checklist != null)
                    {
                        var C = SV.Checklist;
                        if (C.isPointContourChecked)
                        {
                            var VolParts = await SV.EclipseStructure.PartVolumes();
                            var MinVol = double.NaN;
                            if (VolParts != null)
                                MinVol = VolParts.Min();
                            bool Warning = false;
                            string RefValString = string.Format("{0:0.##} cc", C.PointContourVolumeThreshold);
                            string CheckValString = "";
                            string WarningString = "";
                            var NumDetectedParts = await SV.EclipseStructure.NumParts();
                            var VMS_NumParts = await SV.EclipseStructure.VMS_NumParts();

                            if (double.IsNaN(MinVol))
                            {
                                Warning = true;
                                WarningString = "Structure not found";
                            }
                            else if (MinVol < C.PointContourVolumeThreshold)
                            {
                                Warning = true;
                                CheckValString = string.Format("{0:0.##} cc", MinVol);
                                WarningString = "Subvolume less than threshold";
                            }
                            else if (VMS_NumParts > NumDetectedParts)
                            {
                                Warning = true;
                                CheckValString = "< 0.01 cc";
                                WarningString = "Potential point contour";
                            }
                            else
                                CheckValString = string.Format("{0:0.##} cc", MinVol);
                            Targets_ViewModel.Tests.Add(new Controls.TestListItem(string.Format(@"Min. Subvolume (""{0}"")", SV.ProtocolStructureName, SV.EclipseStructure.Id), CheckValString, RefValString, Warning, WarningString));
                        }
                    }


                }

                isPlanCheckCalculating = false;
            }
        }

        private void BLI_PropertyChanged(object sender, EventArgs e, Ctr.ComponentView CV)
        {
            //Refresh Field col separation check

            if (Beam_ViewModel.Beams.Any(x => x.Field == null) || CV.MaxBeams < 2 || double.IsNaN(CV.MinColOffset))
            {
                return;
            }
            var ColOffset = Beam_ViewModel.Beams.Select(x => x.Field).Select(x => x.CollimatorAngle).ToList();
            var MinColOffset = findMinDiff(ColOffset.ToArray());
            var OldTest = Beam_ViewModel.GroupTests.Tests.Where(x => x.TestName == "Min collimator offset").FirstOrDefault();
            Beam_ViewModel.GroupTests.Tests.Remove(OldTest);
            var MinColOFfsetWarning = MinColOffset < CV.MinColOffset;
            Controls.TestListItem MinColOffsetCheck = new Controls.TestListItem("Min collimator offset", string.Format("{0:0.#}", MinColOffset), string.Format("{0:0.#}", CV.MinColOffset),
                MinColOFfsetWarning, "");
            Beam_ViewModel.GroupTests.Tests.Add(MinColOffsetCheck);

        }
        private double findMinDiff(double[] arr)
        {
            // Sort array in  
            // non-decreasing order 
            Array.Sort(arr);
            var n = arr.Length;
            // Initialize difference 
            // as infinite 
            double diff = double.MaxValue;

            // Find the min diff by  
            // comparing adjacent pairs 
            // in sorted array 
            for (int i = 0; i < n - 1; i++)
                if (arr[i + 1] - arr[i] > 180)
                {
                    var val = ((360 - arr[i + 1]) + arr[i]);
                    if (val > 90)
                        val = 180 - val;
                    if (val < diff)
                    {
                        diff = val;
                    }
                }
                else
                    if (arr[i + 1] - arr[i] < diff)
                    diff = arr[i + 1] - arr[i];

            // Return min diff 
            return diff;
        }
        private async void DeleteSelectedSession(object param = null)
        {
            if (isLoading == true)
                return;
            Ctr.SessionView SV = param as Ctr.SessionView;
            if (param == null)
                return;
            isLoading = true;
            LoadingString = "Deleting session...";
            try
            {
                await Task.Run(() => Ctr.Delete_Session(SV.ID));
            }
            catch (Exception ex)
            {
                var t = ex;
            }
            isLoading = false;
        }
        private void SaveSessionDialog(object param = null)
        {
            SessionSaveVisibility ^= true;
        }
        private async void LoadSelectedSession(object param = null)
        {
            if (isLoading == true)
                return;
            Ctr.SessionView SV = param as Ctr.SessionView;
            if (param == null)
                return;
            isLoading = true;
            LoadingString = "Loading session...";
            if (await Task.Run(() => Ctr.Load_Session(SV.ID)))
            {
                AssessmentPresenter = new AssessmentsView();
                UpdateAssessmentsView();
                UpdateProtocolView();
                isLinkProtocolVisible = true;
            }
            else
                MessageBox.Show("Error loading session");
            isLoading = false;
            SessionSelectVisibility ^= true;
        }
        private void LoadSession(object param = null)
        {
            SessionSelectVisibility ^= true;
        }
        private async void SaveSession(object param = null)
        {
            LoadingString = "Saving Session...";
            isLoading = true;
            bool Success = await Task.Run(() => Ctr.Save_Session(SessionsPresenter.SessionComment)); // boolean return is in order to delay the "isLoading" return to False, so the load menu has a chance to include the latest save
            isLoading = false;
            SessionSaveVisibility ^= true;
        }
        private void LaunchAdminView(object param = null)
        {
            try
            {
                var AdminWindow = new AdminWindow();
                AdminWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0} {1} {2}", ex.Message, ex.InnerException, ex.StackTrace));
            }
        }
        private void DeleteConstraint(object param = null)
        {
            var CS = param as ConstraintSelector;
            if (CS != null)
            {
                if (CS.Id > 0)
                {
                    var D = MessageBox.Show("Delete this constraint?", "Confirm deletion", MessageBoxButton.OKCancel);
                    if (D == MessageBoxResult.OK)
                        Ctr.DeleteConstraint(CS.Id);
                }
                else
                    Ctr.DeleteConstraint(CS.Id);
            }
        }
        private void AddConstraintBelow(object param = null)
        {
            ConstraintSelector CS = param as ConstraintSelector;
            if (CS != null)
            {
                Ctr.ConstraintView CV = Ctr.DuplicateConstraint(CS.Id);
            }
        }
        private void ShowComponent(object param = null)
        {
            var CS = (ComponentSelector)param;
            CS.Pinned = !CS.Pinned;
        }
        private void ShowConfig(object param = null)
        {
            if (Ctr.SquintUser == "nchng")
                isConfigVisible = !isConfigVisible;
        }
        private void AddConstraint(object param = null)
        {
            if (isProtocolLoaded)
            {
                Protocol.AddConstraint();
            }
        }
        private void AddStructure(object param = null)
        {
            if (isProtocolLoaded)
            {
                Protocol.AddStructure();
            }
        }
        private void ShiftConstraintUp(object param = null)
        {
            ConstraintSelector CS = param as ConstraintSelector;
            if (CS != null)
            {
                Ctr.ShiftConstraintUp(CS.Id);
            }
        }
        private void ShiftConstraintDown(object param = null)
        {
            ConstraintSelector CS = param as ConstraintSelector;
            if (CS != null)
            {
                Ctr.ShiftConstraintDown(CS.Id);
            }
        }
        public ICommand ShowAssessmentCommand
        {
            get { return new DelegateCommand(ShowAssessment); }
        }
        private void ShowAssessment(object param = null)
        {
            var AS = (AssessmentView)param;
            AS.Pinned = !AS.Pinned;
        }
        public ICommand GetConstraintInformationCommand
        {
            get { return new DelegateCommand(GetConstraintInformation); }
        }
        public void GetConstraintInformation(object param = null)
        {
            var CS = (ConstraintSelector)param;
            CS.ConstraintInfoVisibility ^= true;
        }
        public ICommand PinConstraintCommand
        {
            get { return new DelegateCommand(PinConstraintDetails); }
        }
        private void PinConstraintDetails(object param = null)
        {
            var CS = (ConstraintSelector)param;
            CS.Pinned = !CS.Pinned;
        }
        public ICommand DeleteAssessmentCommand
        {
            get { return new DelegateCommand(DeleteAssessment); }
        }
        private void DeleteAssessment(object param = null)
        {
            var AS = (AssessmentView)param;
            AssessmentPresenter.DeleteAssessment(AS);
            AS.Delete();
        }
        public ICommand UpdateProtocolCommand
        {
            get { return new DelegateCommand(UpdateProtocol); }
        }
        private async void UpdateProtocol(object param = null)
        {
            LoadingString = "Validating changes...";
            bool areChangeDescriptionsComplete = true;
            List<string> IncompleteChangeDefinitions = new List<string>() { "Please enter Change Descriptions for the following modified constraints:" };
            foreach (ConstraintSelector CS in Protocol.Constraints.Where(x => x.isModified == true))
            {
                if (CS.ChangeDescription == "")
                {
                    areChangeDescriptionsComplete = false;
                    IncompleteChangeDefinitions.Add(CS.ShortConstraintDefinition);
                }
            }
            if (!areChangeDescriptionsComplete)
            {
                MessageBox.Show(string.Join(Environment.NewLine, IncompleteChangeDefinitions));
                return;
            }

            LoadingString = "Updating Protocol...";
            isLoading = true;
            await Task.Run(() => Ctr.Save_UpdateProtocol());
            isLoading = false;
            LoadSelectedProtocol(Protocol.ProtocolName);
        }
        public ICommand DuplicateProtocolCommand
        {
            get { return new DelegateCommand(DuplicateProtocol); }
        }
        private void DuplicateProtocol(object param = null)
        {
            Ctr.Save_DuplicateProtocol();
            LoadSelectedProtocol(Protocol.ProtocolName);
        }
        public ICommand DeleteSelectedProtocolCommand
        {
            get { return new DelegateCommand(DeleteSelectedProtocol); }
        }
        private async void DeleteSelectedProtocol(object param = null)
        {
            var PS = (param as ProtocolView.ProtocolSelector);
            LoadingString = "Deleting selected protocol...";
            isLoading = true;
            if (PS != null)
            {
                var Result = MessageBox.Show(string.Format("Are you sure you want to delete this protocol ({0})", PS.ProtocolName), "Confirm deletion", MessageBoxButton.OKCancel);
                if (Result == MessageBoxResult.OK)
                    await Task.Run(() => Ctr.DeleteProtocol(PS.ID));
            }
            isLoading = false;
        }
        private void LoadPatient(object param = null)
        {
            try
            {
                if (PatientPresenter.ParentView == null)
                    PatientPresenter.ParentView = this; // pass the current view so it can access the isLoading variable
                if (Ctr.PatientLoaded)
                {
                    var Result = MessageBox.Show("Close current patient and all assessments?", "Close Patient?", MessageBoxButton.OKCancel);
                    if (Result == MessageBoxResult.Cancel)
                        return;
                    CloseCheckList();
                    Ctr.ClosePatient();
                    AssessmentPresenter = new AssessmentsView();
                    Protocol.Unsubscribe();
                    Ctr.CloseProtocol();
                    Protocol = new ProtocolView();
                }
                Ctr.LoadPatientFromDatabase(PatientPresenter.PatientId);
                SessionsPresenter = new SessionsView();
                if (Ctr.PatientLoaded)
                    PatientPresenter.TextBox_Background_Color = new System.Windows.Media.SolidColorBrush(wpfcolors.AliceBlue);
                else
                    PatientPresenter.TextBox_Background_Color = new System.Windows.Media.SolidColorBrush(wpfcolors.DarkOrange);
                foreach (string CourseId in Ctr.GetCourseNames())
                {
                    PatientPresenter.Courses.Add(new CourseSelector(CourseId));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0} {1} {2}", ex.Message, ex.InnerException, ex.StackTrace));
            }
        }
        private static EventHandler SynchronizeHandler;
        private void SynchronizePatient(object param = null)
        {
            if (Ctr.PatientLoaded && Ctr.ProtocolLoaded && Ctr.GetAssessmentViewList().Count > 0)
            {
                var W = new WaitWindow();
                W.Show();
                SynchronizeHandler = new EventHandler((sender, e) => OnSynchronizationComplete(sender, e, W));
                Ctr.SynchronizationComplete += SynchronizeHandler;
                Ctr.SynchronizePlans();

            }
        }
        private void OnSynchronizationComplete(object sender, EventArgs E, Window W)
        {
            Ctr.SynchronizationComplete -= SynchronizeHandler;
            // Refresh ViewModel
            PatientPresenter.StructureSets.Clear();
            foreach (AssessmentView AV in AssessmentPresenter.Assessments)
            {
                foreach (AssessmentComponentView ACV in AV.ACVs)
                {
                    //ACV.DisableAutomaticAssociation = true; // this kludge stops the setting of the SelectedPlan field from recalculating dose, which is otherwise baked into the setter.  
                    ObservableCollection<CourseSelector> UpdatedCourses = new ObservableCollection<CourseSelector>();
                    foreach (string CourseName in Ctr.GetCourseNames())
                    {
                        UpdatedCourses.Add(new CourseSelector(CourseName));
                    }
                    string PrevSelectedCourseId = "";
                    string PrevSelectedPlanId = "";
                    if (ACV.SelectedCourse != null)
                        PrevSelectedCourseId = ACV.SelectedCourse.CourseId;
                    if (ACV.SelectedPlan != null)
                        PrevSelectedPlanId = ACV.SelectedPlan.PlanId;
                    ACV.Courses = UpdatedCourses;
                    if (PrevSelectedCourseId != "")
                        ACV.SelectedCourse = UpdatedCourses.FirstOrDefault(x => PrevSelectedCourseId == x.CourseId);
                    if (PrevSelectedPlanId != null)
                        ACV.SelectedPlan = ACV.Plans.FirstOrDefault(x => PrevSelectedPlanId == x.PlanId);
                    //ACV.DisableAutomaticAssociation = false;
                }
            }
            W.Close();
        }
        private void AddAssessment(object param = null)
        {
            AssessmentPresenter.AddAssessment();
        }
        private void ChangeVisibility(object param = null)
        {
            if (isPIDVisible && Ctr.PatientLoaded)
            {
                var Result = MessageBox.Show("Close current patient and all assessments?", "Close Patient?", MessageBoxButton.OKCancel);
                if (Result == MessageBoxResult.Cancel)
                    return;
                Ctr.ClosePatient();
                CloseCheckList();
                PatientPresenter = new PatientView();
                AssessmentPresenter = new AssessmentsView();
                Protocol.Unsubscribe();
                Ctr.CloseProtocol();
                Protocol = new ProtocolView();
            }
            isPIDVisible = !isPIDVisible;
        }
        private void ChangeLinkProtocolVisibility(object param = null)
        {
            isLinkProtocolVisible = !isLinkProtocolVisible;
            // Add an assessment if there aren't any
            if (AssessmentPresenter.Assessments.Count == 0 && isLinkProtocolVisible == true)
                AssessmentPresenter.AddAssessment();
        }
        private void ExpandLoadProtocol(object param = null)
        {
            isLoadProtocolPanelVisible = !isLoadProtocolPanelVisible;
        }
        private void ExpandStructures(object param = null)
        {
            isStructurePanelVisible = !isStructurePanelVisible;
        }
        private void ExpandUserPanel(object param = null)
        {
            isUserPanelVisible = !isUserPanelVisible;
        }
        private async void LoadSelectedProtocol(object param)
        {
            if (isLoading == true)
                return;
            string ProtocolId = (string)param;
            if (ProtocolId == null)
                return; // no protocol selected;
            LoadingString = "Loading selected protocol...";
            isLoading = true;
            AssessmentPresenter = new AssessmentsView();
            await Task.Run(() => Ctr.LoadProtocolFromDb(ProtocolId, Protocol.Progress as IProgress<int>));
            UpdateProtocolView();
            isLoading = false;
        }
        private void UpdateAssessmentsView()
        {
            AssessmentPresenter = new AssessmentsView();
            AssessmentPresenter.LoadAssessmentViews();
        }
        private void UpdateProtocolView()
        {
            if (Protocol != null)
                Protocol.Unsubscribe();
            Protocol = new ProtocolView();
            Protocol.Structures.Clear();
            Protocol.Constraints.Clear();
            foreach (var S in Ctr.GetStructureViewList().OrderBy(x => x.DisplayOrder))
            {
                Protocol.Structures.Add(new StructureSelector(S));
            }
            foreach (var C in Ctr.GetConstraintViewList().OrderBy(x => x.DisplayOrder))
            {
                StructureSelector SS = Protocol.Structures.FirstOrDefault(x => x.Id == C.PrimaryStructureID);
                ConstraintSelector CS = new ConstraintSelector(C, SS);
                Protocol.Constraints.Add(CS);
            }
            foreach (var CompV in Ctr.GetComponentViewList().OrderBy(x => x.DisplayOrder))
            {
                Protocol.Components.Add(new ComponentSelector(CompV));
            }
            try
            {
                isProtocolLoaded = true;
            }
            catch (Exception ex)
            {
                string debugme = "hi";
            }
        }
        private void OnConstraintPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // This captures changes to constraint properties that affect the layout of the constraints, i.e. displayorder

        }
        private void FontSizeIncrease(object param = null)
        {
            var AP = param as AssessmentsView;
            if (AP != null)
                AP.FontSize = AP.FontSize + 1;
        }
        private void FontSizeDecrease(object param = null)
        {
            var AP = param as AssessmentsView;
            if (AP != null)
            {
                AP.FontSize = AP.FontSize - 1;
                foreach (SquintDataColumn C in AssessmentPresenter.AssessmentColumns)
                {
                    C.Width = new DataGridLength(0);
                    C.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
                }
                AssessmentPresenter.RowHeaderWidth = 0;
                AssessmentPresenter.RowHeaderWidth = double.NaN;
                // AssessmentPresenter.RowHeaderWidth = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
    }
}
