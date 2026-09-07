import React, { useState, useRef } from 'react';
import { useApp } from '../context/AppContext';
import {
  FileDown,
  AlertTriangle,
  Heart,
  Activity,
  Calendar,
  FileText,
  UploadCloud,
  FileCheck,
  Eye,
  Download,
  Plus,
  Sparkles,
  CheckCircle,
} from 'lucide-react';

export const PatientProfileView: React.FC = () => {
  const {
    lang,
    patient,
    addPatientFile,
    addTimelineEntry,
    setPreviewFile,
    addToast,
  } = useApp();

  const isAr = lang === 'ar';
  const [activeTab, setActiveTab] = useState<'diagnoses' | 'notes' | 'labs'>('diagnoses');
  const [isAddingNote, setIsAddingNote] = useState(false);
  const [newNoteTitle, setNewNoteTitle] = useState('');
  const [newNoteContent, setNewNoteContent] = useState('');
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleExportPDF = () => {
    addToast(
      isAr ? 'جاري تصدير الملف الطبي' : 'Exporting Medical File',
      isAr ? `تم تصدير ملف ${patient.name} بصيغة PDF بنجاح` : `Exported ${patient.nameEn} EMR as PDF`,
      'success'
    );
  };

  const handleCreateNote = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newNoteTitle || !newNoteContent) return;

    addTimelineEntry({
      title: newNoteTitle,
      titleEn: newNoteTitle,
      time: isAr ? 'اليوم، الآن' : 'Today, Just now',
      timeEn: 'Today, Just now',
      doctor: isAr ? 'د. سارة آدمن • الطب الباطني' : 'Dr. Sarah Admin • Internal Med',
      doctorEn: 'Dr. Sarah Admin • Internal Med',
      doctorAvatar: patient.avatar,
      content: newNoteContent,
      contentEn: newNoteContent,
      isSystem: false,
    });

    setNewNoteTitle('');
    setNewNoteContent('');
    setIsAddingNote(false);
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      const file = files[0];
      const isImg = file.type.startsWith('image/');
      const sizeMb = (file.size / (1024 * 1024)).toFixed(1) + ' MB';
      const fileUrl = URL.createObjectURL(file);

      addPatientFile({
        name: file.name,
        size: sizeMb,
        date: isAr ? 'اليوم' : 'Today',
        dateEn: `Today • ${sizeMb}`,
        type: isImg ? 'image' : 'pdf',
        url: fileUrl,
      });
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      const file = e.dataTransfer.files[0];
      const isImg = file.type.startsWith('image/');
      const sizeMb = (file.size / (1024 * 1024)).toFixed(1) + ' MB';
      const fileUrl = URL.createObjectURL(file);

      addPatientFile({
        name: file.name,
        size: sizeMb,
        date: isAr ? 'اليوم' : 'Today',
        dateEn: `Today • ${sizeMb}`,
        type: isImg ? 'image' : 'pdf',
        url: fileUrl,
      });
    }
  };

  return (
    <div className="flex-1 overflow-y-auto p-4 md:p-6 lg:p-8 custom-scrollbar">
      <div className="max-w-[1440px] mx-auto flex flex-col gap-6">
        {/* Bento Patient Info Header */}
        <section
          id="patient-header-card"
          className="bg-white rounded-xl border border-[#E2E8F0] p-6 shadow-2xs"
        >
          <div className="flex flex-col lg:flex-row justify-between gap-6 items-start lg:items-center">
            {/* Left: Avatar & Basic Details */}
            <div className="flex items-start gap-4">
              <div className="relative">
                <img
                  src={patient.avatar}
                  alt={isAr ? patient.name : patient.nameEn}
                  className="w-16 h-16 rounded-full object-cover border-2 border-[#CBD5E1]"
                />
                <span className="absolute bottom-0 end-0 w-3.5 h-3.5 rounded-full bg-[#166534] border-2 border-white" />
              </div>

              <div>
                <div className="flex items-center gap-3">
                  <h3 className="text-[22px] font-bold text-[#0F172A]">
                    {isAr ? patient.name : patient.nameEn}
                  </h3>
                  <span className="px-2 py-0.5 rounded bg-[#eaedff] text-[#006194] text-[11px] font-semibold" dir="ltr">
                    {patient.mrn}
                  </span>
                </div>

                <p className="text-[13px] text-[#64748B] mt-1">
                  {isAr
                    ? `${patient.gender}، ${patient.age} سنة • تاريخ الميلاد: ${patient.dob}`
                    : `${patient.genderEn}, ${patient.age} yrs • DOB: ${patient.dobEn}`}
                </p>

                {/* Allergies Chips */}
                <div className="flex flex-wrap items-center gap-2 mt-3">
                  <span className="text-[11px] font-semibold text-[#64748B] uppercase">
                    {isAr ? 'الحساسية:' : 'Allergies:'}
                  </span>
                  {patient.allergies.map((allergy, i) => (
                    <span
                      key={i}
                      className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${
                        allergy.type === 'warning'
                          ? 'bg-[#FEE2E2] text-[#991B1B] border border-[#FECACA]'
                          : 'bg-[#FEF3C7] text-[#92400E] border border-[#FDE68A]'
                      }`}
                    >
                      <AlertTriangle className="w-3 h-3" />
                      <span>{isAr ? allergy.name : allergy.nameEn}</span>
                    </span>
                  ))}
                </div>
              </div>
            </div>

            {/* Right: Key Vitals & Export Action */}
            <div className="flex flex-wrap items-center gap-3 w-full lg:w-auto">
              {/* Vital 1: Blood Pressure */}
              <div className="bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg p-3 min-w-[130px] flex-1 sm:flex-initial">
                <div className="flex items-center gap-1.5 text-[11px] font-semibold text-[#64748B] mb-1">
                  <Activity className="w-3.5 h-3.5 text-[#006194]" />
                  <span>{isAr ? 'ضغط الدم' : 'Blood Pressure'}</span>
                </div>
                <div className="text-[18px] font-bold text-[#0F172A]" dir="ltr">
                  {patient.bp}{' '}
                  <span className="text-[11px] font-normal text-[#64748B]">mmHg</span>
                </div>
              </div>

              {/* Vital 2: Heart Rate */}
              <div className="bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg p-3 min-w-[130px] flex-1 sm:flex-initial">
                <div className="flex items-center gap-1.5 text-[11px] font-semibold text-[#64748B] mb-1">
                  <Heart className="w-3.5 h-3.5 text-[#ba1a1a]" />
                  <span>{isAr ? 'نبض القلب' : 'Heart Rate'}</span>
                </div>
                <div className="text-[18px] font-bold text-[#0F172A]" dir="ltr">
                  {patient.hr}{' '}
                  <span className="text-[11px] font-normal text-[#64748B]">bpm</span>
                </div>
              </div>

              {/* Export PDF Button */}
              <button
                id="btn-export-pdf"
                onClick={handleExportPDF}
                className="h-10 px-4 bg-[#006194] hover:bg-[#004b73] text-white text-[13px] font-medium rounded-lg transition-all flex items-center justify-center gap-2 shadow-sm cursor-pointer active:scale-98"
              >
                <FileDown className="w-4 h-4" />
                <span>{isAr ? 'تصدير PDF' : 'Export PDF'}</span>
              </button>
            </div>
          </div>
        </section>

        {/* EMR Main Content Grid (8 Cols Timeline + 4 Cols Files) */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Clinical Records (8 Cols) */}
          <section
            id="clinical-records-section"
            className="lg:col-span-8 bg-white rounded-xl border border-[#E2E8F0] flex flex-col overflow-hidden shadow-xs"
          >
            {/* Tabs Bar */}
            <div className="flex border-b border-[#E2E8F0] bg-[#F8FAFC] px-4 pt-2">
              <button
                onClick={() => setActiveTab('diagnoses')}
                className={`px-4 py-2.5 text-[13px] font-semibold border-b-2 transition-colors cursor-pointer ${
                  activeTab === 'diagnoses'
                    ? 'border-[#006194] text-[#006194]'
                    : 'border-transparent text-[#64748B] hover:text-[#0F172A]'
                }`}
              >
                {isAr ? 'التشخيصات والزيارات' : 'Diagnoses & Visits'}
              </button>
              <button
                onClick={() => setActiveTab('notes')}
                className={`px-4 py-2.5 text-[13px] font-semibold border-b-2 transition-colors cursor-pointer ${
                  activeTab === 'notes'
                    ? 'border-[#006194] text-[#006194]'
                    : 'border-transparent text-[#64748B] hover:text-[#0F172A]'
                }`}
              >
                {isAr ? 'الملاحظات السريرية' : 'Clinical Notes'}
              </button>
              <button
                onClick={() => setActiveTab('labs')}
                className={`px-4 py-2.5 text-[13px] font-semibold border-b-2 transition-colors cursor-pointer ${
                  activeTab === 'labs'
                    ? 'border-[#006194] text-[#006194]'
                    : 'border-transparent text-[#64748B] hover:text-[#0F172A]'
                }`}
              >
                {isAr ? 'نتائج المختبر' : 'Lab Results'}
              </button>
            </div>

            {/* Timeline Area */}
            <div className="p-6 flex-1">
              <div className="flex justify-between items-center mb-6">
                <h4 className="text-[17px] font-bold text-[#0F172A]">
                  {isAr ? 'الجدول الزمني السريري' : 'Clinical Timeline'}
                </h4>
                <button
                  id="btn-add-clinical-note"
                  onClick={() => setIsAddingNote(!isAddingNote)}
                  className="text-[12px] font-semibold text-[#006194] hover:bg-[#eaedff] px-2.5 py-1 rounded transition-colors flex items-center gap-1 cursor-pointer"
                >
                  <Plus className="w-3.5 h-3.5" />
                  <span>{isAr ? 'إضافة ملاحظة' : 'Add Note'}</span>
                </button>
              </div>

              {/* Add Note Inline Form */}
              {isAddingNote && (
                <form
                  onSubmit={handleCreateNote}
                  className="mb-6 p-4 bg-[#F8FAFC] border border-[#CBD5E1] rounded-lg animate-in fade-in"
                >
                  <h5 className="text-[13px] font-bold text-[#0F172A] mb-2">
                    {isAr ? 'ملاحظة سريرية جديدة' : 'New Clinical Note'}
                  </h5>
                  <input
                    type="text"
                    required
                    value={newNoteTitle}
                    onChange={(e) => setNewNoteTitle(e.target.value)}
                    placeholder={isAr ? 'عنوان التقرير (مثل: تقرير فحص المتابعة)' : 'Title (e.g., Follow-up Consultation)'}
                    className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-1.5 text-[13px] mb-2 text-[#0F172A]"
                  />
                  <textarea
                    required
                    rows={3}
                    value={newNoteContent}
                    onChange={(e) => setNewNoteContent(e.target.value)}
                    placeholder={isAr ? 'اكتب الملاحظات الطبية والتوصيات العلاجية...' : 'Write clinical observations and treatment plan...'}
                    className="w-full bg-white border border-[#CBD5E1] rounded p-2 text-[13px] mb-3 text-[#0F172A]"
                  />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => setIsAddingNote(false)}
                      className="px-3 py-1 text-[12px] text-[#64748B] hover:text-[#0F172A]"
                    >
                      {isAr ? 'إلغاء' : 'Cancel'}
                    </button>
                    <button
                      type="submit"
                      className="px-3 py-1 bg-[#006194] text-white rounded text-[12px] font-medium"
                    >
                      {isAr ? 'حفظ الملاحظة' : 'Save Note'}
                    </button>
                  </div>
                </form>
              )}

              {/* Timeline Items */}
              <div className="relative border-s-2 border-[#E2E8F0] ps-6 ms-2 space-y-6">
                {patient.timeline.map((entry) => (
                  <div key={entry.id} className="relative group">
                    {/* Circle Node */}
                    <div
                      className={`absolute -start-[31px] top-1 w-3.5 h-3.5 rounded-full border-2 border-white ${
                        entry.isSystem ? 'bg-[#64748B]' : 'bg-[#006194]'
                      }`}
                    />

                    {/* Timeline Item Content Card */}
                    <div className="bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg p-4 hover:border-[#006194]/40 transition-colors">
                      <div className="flex justify-between items-start mb-2">
                        <div className="flex items-center gap-2">
                          {entry.doctorAvatar && (
                            <img
                              src={entry.doctorAvatar}
                              alt="Doctor"
                              className="w-6 h-6 rounded-full object-cover"
                            />
                          )}
                          <div>
                            <h5 className="text-[14px] font-bold text-[#0F172A]">
                              {isAr ? entry.title : entry.titleEn}
                            </h5>
                            <p className="text-[11px] text-[#64748B]">
                              {isAr ? entry.doctor : entry.doctorEn}
                            </p>
                          </div>
                        </div>

                        <span className="text-[11px] text-[#64748B] font-medium" dir="ltr">
                          {isAr ? entry.time : entry.timeEn}
                        </span>
                      </div>

                      <p className="text-[13px] text-[#334155] leading-relaxed">
                        {isAr ? entry.content : entry.contentEn}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </section>

          {/* Medical Files & Attachments (4 Cols) */}
          <aside
            id="medical-files-section"
            className="lg:col-span-4 flex flex-col gap-6"
          >
            {/* Upload & Files Card */}
            <div className="bg-white rounded-xl border border-[#E2E8F0] p-5 shadow-xs">
              <h4 className="text-[16px] font-bold text-[#0F172A] mb-3">
                {isAr ? 'الملفات والمرفقات الطبية' : 'Medical Files & Attachments'}
              </h4>

              {/* Drag and Drop Zone */}
              <div
                onDragOver={(e) => {
                  e.preventDefault();
                  setIsDragging(true);
                }}
                onDragLeave={() => setIsDragging(false)}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
                className={`border-2 border-dashed rounded-lg p-5 text-center cursor-pointer transition-colors ${
                  isDragging
                    ? 'border-[#006194] bg-[#eaedff]'
                    : 'border-[#CBD5E1] bg-[#F8FAFC] hover:bg-[#F1F5F9]'
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  onChange={handleFileUpload}
                  className="hidden"
                  accept="image/*,.pdf,.dcm"
                />
                <UploadCloud className="w-8 h-8 text-[#006194] mx-auto mb-2" />
                <p className="text-[12px] font-semibold text-[#0F172A]">
                  {isAr
                    ? 'أسقط الملفات الطبية هنا أو انقر للتصفح'
                    : 'Drop medical files here or click to browse'}
                </p>
                <p className="text-[11px] text-[#64748B] mt-1">
                  {isAr
                    ? 'يدعم DICOM، PDF، JPG (حتى 50 ميغابايت)'
                    : 'Supports DICOM, PDF, JPG (up to 50MB)'}
                </p>
              </div>

              {/* Files List */}
              <div className="mt-4 space-y-2">
                {patient.files.map((file) => (
                  <div
                    key={file.id}
                    className="p-3 bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg flex items-center justify-between group hover:border-[#006194]/30 transition-all"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded bg-[#eaedff] text-[#006194] flex items-center justify-center shrink-0">
                        {file.type === 'image' ? (
                          <span className="material-symbols-outlined text-[18px]">image</span>
                        ) : (
                          <FileText className="w-4 h-4" />
                        )}
                      </div>
                      <div className="min-w-0">
                        <p className="text-[12px] font-bold text-[#0F172A] truncate">
                          {file.name}
                        </p>
                        <p className="text-[11px] text-[#64748B]" dir="ltr">
                          {isAr ? file.date : file.dateEn}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-1">
                      {file.type === 'image' && (
                        <button
                          onClick={() => setPreviewFile(file)}
                          className="p-1 text-[#006194] hover:bg-[#eaedff] rounded cursor-pointer"
                          title={isAr ? 'معاينة' : 'Preview'}
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                      )}
                      <button
                        onClick={() =>
                          addToast(
                            isAr ? 'تنزيل الملف' : 'Downloading File',
                            file.name,
                            'info'
                          )
                        }
                        className="p-1 text-[#64748B] hover:text-[#0F172A] rounded cursor-pointer"
                        title={isAr ? 'تنزيل' : 'Download'}
                      >
                        <Download className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Quick Status Bento Widget */}
            <div className="bg-white rounded-xl border border-[#E2E8F0] p-4 shadow-xs">
              <div className="flex justify-between items-center mb-3">
                <span className="text-[12px] font-semibold text-[#64748B]">
                  {isAr ? 'حالة الملف الطبي' : 'EMR Status'}
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#DCFCE7] text-[#166534] text-[11px] font-bold">
                  <CheckCircle className="w-3 h-3" />
                  {isAr ? 'نشط' : 'Active'}
                </span>
              </div>
              <div className="text-[12px] text-[#64748B]">
                <p className="font-medium text-[#0F172A]">
                  {isAr ? 'الموعد القادم:' : 'Next Appointment:'}
                </p>
                <p className="mt-0.5">
                  {isAr ? patient.nextApptDate : patient.nextApptDateEn} (
                  {isAr ? patient.nextApptDoctor : patient.nextApptDoctorEn})
                </p>
              </div>
            </div>
          </aside>
        </div>
      </div>
    </div>
  );
};
