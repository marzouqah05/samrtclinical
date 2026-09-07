import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import {
  TrendingUp,
  TrendingDown,
  Users,
  CloudCheck,
  ChevronLeft,
  ChevronRight,
  MoreHorizontal,
  CheckCircle2,
  Clock,
  UserCheck,
  ArrowRight,
  Plus,
} from 'lucide-react';

export const DashboardView: React.FC = () => {
  const {
    lang,
    appointments,
    queue,
    updateQueueStatus,
    setIsApptModalOpen,
    setActiveTab,
    addToast,
  } = useApp();

  const isAr = lang === 'ar';
  const [selectedDate, setSelectedDate] = useState<string>(isAr ? 'اليوم' : 'Today');
  const [activeQueueFilter, setActiveQueueFilter] = useState<'all' | 'waiting' | 'active'>('all');
  const [showFullQueueModal, setShowFullQueueModal] = useState(false);

  return (
    <div className="flex-1 overflow-y-auto p-4 md:p-6 lg:p-8 custom-scrollbar">
      <div className="max-w-[1440px] mx-auto flex flex-col gap-6">
        {/* KPI Grid */}
        <section id="kpi-section" className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* KPI 1: Total Appointments */}
          <div
            id="kpi-appointments"
            className="bg-white rounded-xl p-4 border border-[#E2E8F0] flex flex-col justify-between h-32 relative overflow-hidden group shadow-2xs"
          >
            <div className="flex justify-between items-start z-10">
              <span className="text-[13px] font-medium text-[#64748B]">
                {isAr ? 'إجمالي المواعيد' : 'Total Appointments'}
              </span>
              <span
                className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-[#DCFCE7] text-[#166534] text-[11px] font-semibold"
                dir="ltr"
              >
                <TrendingUp className="w-3 h-3" />
                +12%
              </span>
            </div>
            <div className="text-[28px] font-bold text-[#0F172A] z-10" dir="ltr">
              240
            </div>
            {/* Sparkline Abstract */}
            <div className="absolute bottom-0 inset-x-0 h-12 opacity-20 bg-gradient-to-t from-[#006194]/30 to-transparent pointer-events-none" />
            <svg
              className="absolute bottom-0 inset-x-0 w-full h-12 text-[#006194] opacity-30 pointer-events-none"
              preserveAspectRatio="none"
              viewBox="0 0 100 30"
            >
              <path
                d="M0,30 L0,25 Q10,20 20,25 T40,15 T60,20 T80,5 L100,10 L100,30 Z"
                fill="currentColor"
              />
            </svg>
          </div>

          {/* KPI 2: Revenue */}
          <div
            id="kpi-revenue"
            className="bg-white rounded-xl p-4 border border-[#E2E8F0] flex flex-col justify-between h-32 relative overflow-hidden group shadow-2xs"
          >
            <div className="flex justify-between items-start z-10">
              <span className="text-[13px] font-medium text-[#64748B]">
                {isAr ? 'الإيرادات' : 'Revenue'}
              </span>
              <span
                className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-[#FEE2E2] text-[#991B1B] text-[11px] font-semibold"
                dir="ltr"
              >
                <TrendingDown className="w-3 h-3" />
                -3%
              </span>
            </div>
            <div className="text-[28px] font-bold text-[#0F172A] z-10" dir="ltr">
              $42.5k
            </div>
            <div className="absolute bottom-0 inset-x-0 h-12 opacity-20 bg-gradient-to-t from-[#ba1a1a]/30 to-transparent pointer-events-none" />
            <svg
              className="absolute bottom-0 inset-x-0 w-full h-12 text-[#ba1a1a] opacity-30 pointer-events-none"
              preserveAspectRatio="none"
              viewBox="0 0 100 30"
            >
              <path
                d="M0,30 L0,5 L20,15 T40,25 T60,20 T80,28 L100,25 L100,30 Z"
                fill="currentColor"
              />
            </svg>
          </div>

          {/* KPI 3: Active Patients */}
          <div
            id="kpi-active-patients"
            className="bg-white rounded-xl p-4 border border-[#E2E8F0] flex flex-col justify-between h-32 relative overflow-hidden shadow-2xs cursor-pointer hover:border-[#006194] transition-colors"
            onClick={() => setActiveTab('patients')}
          >
            <div className="flex justify-between items-start z-10">
              <span className="text-[13px] font-medium text-[#64748B]">
                {isAr ? 'المرضى النشطون' : 'Active Patients'}
              </span>
              <Users className="w-4 h-4 text-[#64748B]" />
            </div>
            <div className="text-[28px] font-bold text-[#0F172A] z-10" dir="ltr">
              1,240
            </div>
            <div className="absolute bottom-0 inset-x-0 h-12 opacity-20 bg-gradient-to-t from-[#006a63]/30 to-transparent pointer-events-none" />
            <svg
              className="absolute bottom-0 inset-x-0 w-full h-12 text-[#006a63] opacity-30 pointer-events-none"
              preserveAspectRatio="none"
              viewBox="0 0 100 30"
            >
              <path
                d="M0,30 L0,20 Q15,25 30,15 T60,10 T85,18 L100,15 L100,30 Z"
                fill="currentColor"
              />
            </svg>
          </div>

          {/* KPI 4: Cloud Backup */}
          <div
            id="kpi-cloud-backup"
            className="bg-white rounded-xl p-4 border border-[#E2E8F0] flex flex-col justify-between h-32 relative overflow-hidden shadow-2xs cursor-pointer hover:border-[#006194] transition-colors"
            onClick={() => setActiveTab('settings')}
          >
            <div className="flex justify-between items-start z-10">
              <span className="text-[13px] font-medium text-[#64748B]">
                {isAr ? 'النسخ الاحتياطي السحابي' : 'Cloud Backup'}
              </span>
              <span className="material-symbols-outlined text-[18px] text-[#64748B]">cloud_done</span>
            </div>
            <div className="flex items-center gap-2 z-10">
              <div className="w-2.5 h-2.5 rounded-full bg-[#166534] animate-pulse" />
              <span className="text-[20px] font-bold text-[#0F172A]">
                {isAr ? 'تمت المزامنة' : 'Synced'}
              </span>
            </div>
            <div className="absolute bottom-0 inset-x-0 h-1 bg-[#166534]/20">
              <div className="h-full bg-[#166534] w-full" />
            </div>
          </div>
        </section>

        {/* Live Schedule Board & Queue Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 min-h-[580px]">
          {/* Live Schedule Board (8 Cols on Desktop) */}
          <section
            id="live-schedule-board"
            className="lg:col-span-8 bg-white rounded-xl border border-[#E2E8F0] flex flex-col overflow-hidden shadow-xs"
          >
            <div className="p-4 border-b border-[#E2E8F0] bg-[#F8FAFC]/70 flex justify-between items-center">
              <div className="flex items-center gap-2">
                <h3 className="text-[18px] font-bold text-[#0F172A]">
                  {isAr ? 'لوحة المواعيد المباشرة' : 'Live Schedule Board'}
                </h3>
                <span className="text-[11px] font-medium px-2 py-0.5 rounded bg-[#eaedff] text-[#006194]">
                  {appointments.length} {isAr ? 'مواعيد' : 'slots'}
                </span>
              </div>

              <div className="flex items-center gap-1.5 bg-white border border-[#E2E8F0] rounded-md px-1 py-0.5">
                <button
                  onClick={() => addToast(isAr ? 'اليوم السابق' : 'Previous Day', '', 'info')}
                  className="p-1 rounded hover:bg-[#F8FAFC] text-[#64748B] hover:text-[#0F172A] transition-colors cursor-pointer"
                >
                  {isAr ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
                </button>
                <span className="text-[12px] font-semibold text-[#0F172A] px-2 select-none">
                  {selectedDate}
                </span>
                <button
                  onClick={() => addToast(isAr ? 'اليوم التالي' : 'Next Day', '', 'info')}
                  className="p-1 rounded hover:bg-[#F8FAFC] text-[#64748B] hover:text-[#0F172A] transition-colors cursor-pointer"
                >
                  {isAr ? <ChevronLeft className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {/* Timeline Canvas */}
            <div className="flex-1 overflow-y-auto p-4 relative min-h-[460px]">
              {/* Red Current Time Line: 10:30 AM */}
              <div
                className="absolute inset-x-0 flex items-center z-20 pointer-events-none"
                style={{ top: '150px' }}
              >
                <div
                  className={`w-20 font-mono text-[11px] text-[#ba1a1a] font-bold ${
                    isAr ? 'text-start ps-4' : 'text-end pe-4'
                  }`}
                  dir="ltr"
                >
                  10:30 AM
                </div>
                <div className="flex-1 h-[2px] bg-[#ba1a1a] relative">
                  <div
                    className={`absolute top-[-3px] w-2 h-2 rounded-full bg-[#ba1a1a] ${
                      isAr ? '-end-1' : '-start-1'
                    }`}
                  />
                </div>
              </div>

              {/* Time Slots */}
              <div className="flex flex-col gap-8">
                {/* Slot 09:00 AM */}
                <div className="flex gap-4 relative">
                  <div
                    className={`w-20 shrink-0 text-[13px] font-medium text-[#64748B] pt-2 ${
                      isAr ? 'text-start' : 'text-end'
                    }`}
                    dir="ltr"
                  >
                    09:00 AM
                  </div>
                  <div className="flex-1 border-t border-[#E2E8F0] relative pt-2 min-h-[75px]">
                    {/* Appointment: Emily Chen */}
                    <div
                      onClick={() => setActiveTab('patients')}
                      className={`absolute top-2 w-[85%] bg-[#eaedff]/60 border border-[#dae2fd] rounded-lg p-2.5 cursor-pointer hover:border-[#006194] transition-all group ${
                        isAr ? 'right-0' : 'left-0'
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="text-[13px] font-bold text-[#0F172A] group-hover:text-[#006194] transition-colors">
                            {isAr ? 'إميلي تشين' : 'Emily Chen'}
                          </p>
                          <p className="text-[12px] text-[#64748B]">
                            {isAr ? 'فحص عام - د. سميث' : 'General Checkup - Dr. Smith'}
                          </p>
                        </div>
                        <span className="w-2 h-2 rounded-full bg-[#CBD5E1] mt-1" />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Slot 10:00 AM */}
                <div className="flex gap-4 relative">
                  <div
                    className={`w-20 shrink-0 text-[13px] font-medium text-[#64748B] pt-2 ${
                      isAr ? 'text-start' : 'text-end'
                    }`}
                    dir="ltr"
                  >
                    10:00 AM
                  </div>
                  <div className="flex-1 border-t border-[#E2E8F0] relative pt-2 min-h-[85px]">
                    {/* Active Appointment: John Doe */}
                    <div
                      onClick={() => setActiveTab('patients')}
                      className={`absolute top-2 w-[75%] bg-[#cce5ff]/40 border border-[#93ccff] rounded-lg p-2.5 cursor-pointer hover:border-[#006194] transition-all group z-10 shadow-xs ${
                        isAr ? 'right-[8%]' : 'left-[8%]'
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="text-[13px] font-bold text-[#006194]">
                            {isAr ? 'جون دو' : 'John Doe'}
                          </p>
                          <p className="text-[12px] text-[#004b73]">
                            {isAr ? 'استشارة قلب - د. آدامز' : 'Cardiology Consult - Dr. Adams'}
                          </p>
                        </div>
                        <span className="w-2 h-2 rounded-full bg-[#006194] mt-1 animate-pulse" />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Slot 11:00 AM */}
                <div className="flex gap-4 relative">
                  <div
                    className={`w-20 shrink-0 text-[13px] font-medium text-[#64748B] pt-2 ${
                      isAr ? 'text-start' : 'text-end'
                    }`}
                    dir="ltr"
                  >
                    11:00 AM
                  </div>
                  <div className="flex-1 border-t border-[#E2E8F0] relative pt-2 min-h-[85px]">
                    {/* Scheduled Appointment: Sarah Ahmed */}
                    <div
                      onClick={() => setActiveTab('patients')}
                      className={`absolute top-2 w-[90%] bg-white border border-[#CBD5E1] border-dashed rounded-lg p-2.5 cursor-pointer hover:border-[#006194] transition-all group ${
                        isAr ? 'right-0' : 'left-0'
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="text-[13px] font-bold text-[#0F172A] group-hover:text-[#006194] transition-colors">
                            {isAr ? 'سارة أحمد' : 'Sarah Ahmed'}
                          </p>
                          <p className="text-[12px] text-[#64748B]">
                            {isAr ? 'متابعة - د. سميث' : 'Follow-up - Dr. Smith'}
                          </p>
                        </div>
                        <span className="w-2 h-2 rounded-full bg-[#ffb875] mt-1" />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Slot 12:00 PM: Lunch Break */}
                <div className="flex gap-4 relative opacity-70">
                  <div
                    className={`w-20 shrink-0 text-[13px] font-medium text-[#64748B] pt-2 ${
                      isAr ? 'text-start' : 'text-end'
                    }`}
                    dir="ltr"
                  >
                    12:00 PM
                  </div>
                  <div
                    className="flex-1 border-t border-[#E2E8F0] relative pt-2 min-h-[44px] flex items-center justify-center rounded"
                    style={{
                      backgroundImage:
                        'repeating-linear-gradient(45deg, transparent, transparent 10px, #f1f5f9 10px, #f1f5f9 20px)',
                    }}
                  >
                    <span className="text-[12px] font-medium text-[#64748B] bg-white px-3 py-0.5 rounded shadow-2xs">
                      {isAr ? 'استراحة غداء' : 'Lunch Break'}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Bottom Quick Action */}
            <div className="p-3 border-t border-[#E2E8F0] bg-[#F8FAFC] flex justify-between items-center">
              <span className="text-[12px] text-[#64748B]">
                {isAr
                  ? 'انقر على أي موعد لعرض الملف الطبي للمريض'
                  : 'Click any appointment to open EMR details'}
              </span>
              <button
                onClick={() => setIsApptModalOpen(true)}
                className="text-[12px] font-semibold text-[#006194] hover:underline flex items-center gap-1 cursor-pointer"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>{isAr ? 'إضافة موعد لليوم' : 'Add Slot'}</span>
              </button>
            </div>
          </section>

          {/* Patient Queue (4 Cols on Desktop) */}
          <aside
            id="patient-queue-section"
            className="lg:col-span-4 bg-white rounded-xl border border-[#E2E8F0] flex flex-col overflow-hidden shadow-xs"
          >
            <div className="p-4 border-b border-[#E2E8F0] bg-[#F8FAFC]/70 flex justify-between items-center">
              <div>
                <h3 className="text-[18px] font-bold text-[#0F172A]">
                  {isAr ? 'قائمة انتظار المرضى' : 'Patient Queue'}
                </h3>
                <p className="text-[12px] text-[#64748B] mt-0.5">
                  {queue.filter((q) => q.status !== 'completed').length}{' '}
                  {isAr ? 'مرضى في المنشأة' : 'patients in facility'}
                </p>
              </div>

              <button
                onClick={() => setIsApptModalOpen(true)}
                className="p-1 rounded text-[#006194] hover:bg-[#eaedff] transition-colors"
                title={isAr ? 'إضافة مريض للطابور' : 'Add to queue'}
              >
                <Plus className="w-4 h-4" />
              </button>
            </div>

            {/* Queue List */}
            <div className="flex-1 overflow-y-auto divide-y divide-[#E2E8F0]">
              {queue.map((item) => {
                const isWaiting = item.status === 'waiting';
                const isInConsult = item.status === 'in_consultation';
                const isCompleted = item.status === 'completed';

                return (
                  <div
                    key={item.id}
                    className={`p-4 transition-colors cursor-pointer group relative ${
                      isInConsult
                        ? 'bg-[#006194]/5 hover:bg-[#006194]/10'
                        : isCompleted
                        ? 'opacity-60 hover:bg-[#F8FAFC]'
                        : 'hover:bg-[#F8FAFC]'
                    }`}
                  >
                    {/* Active Accent Bar */}
                    {isInConsult && (
                      <div
                        className={`absolute top-0 bottom-0 w-1 bg-[#006194] ${
                          isAr ? 'right-0' : 'left-0'
                        }`}
                      />
                    )}

                    <div className="flex justify-between items-center mb-1.5">
                      {isWaiting && (
                        <span className="inline-flex items-center px-2 py-0.5 rounded bg-[#ffdcc0]/50 text-[#894d00] text-[11px] font-semibold">
                          {isAr
                            ? item.waitingDuration || 'قيد الانتظار (15 دقيقة)'
                            : item.waitingDurationEn || 'Waiting (15m)'}
                        </span>
                      )}

                      {isInConsult && (
                        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-[#cce5ff] text-[#004b73] text-[11px] font-semibold">
                          <span className="w-1.5 h-1.5 rounded-full bg-[#006194] animate-pulse" />
                          {isAr ? 'في استشارة' : 'In Consultation'}
                        </span>
                      )}

                      {isCompleted && (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-[#E2E8F0] text-[#64748B] text-[11px] font-medium">
                          <CheckCircle2 className="w-3 h-3" />
                          {isAr ? 'مكتمل' : 'Completed'}
                        </span>
                      )}

                      {/* Status Action Buttons */}
                      <div className="flex items-center gap-1 opacity-80 group-hover:opacity-100">
                        {isWaiting && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              updateQueueStatus(item.id, 'in_consultation');
                            }}
                            className="text-[11px] font-semibold text-[#006194] bg-white border border-[#93ccff] px-2 py-0.5 rounded hover:bg-[#eaedff] transition-all cursor-pointer"
                          >
                            {isAr ? 'بدء الكشف' : 'Call In'}
                          </button>
                        )}

                        {isInConsult && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              updateQueueStatus(item.id, 'completed');
                            }}
                            className="text-[11px] font-semibold text-[#166534] bg-[#DCFCE7] border border-[#DCFCE7] px-2 py-0.5 rounded hover:bg-[#bbf7d0] transition-all cursor-pointer"
                          >
                            {isAr ? 'إنهاء' : 'Finish'}
                          </button>
                        )}
                      </div>
                    </div>

                    <h4
                      className={`text-[13px] font-bold transition-colors ${
                        isCompleted ? 'text-[#64748B] line-through' : 'text-[#0F172A] group-hover:text-[#006194]'
                      }`}
                    >
                      {isAr ? item.patientName : item.patientNameEn}
                    </h4>

                    <p className="text-[12px] text-[#64748B] mt-0.5" dir="ltr">
                      {isCompleted
                        ? `Checked out at ${item.checkoutTime || '09:45 AM'}`
                        : isInConsult
                        ? `${item.doctorEn} • ${item.room}`
                        : `Appt: ${item.time} • ${item.room}`}
                    </p>
                  </div>
                );
              })}
            </div>

            {/* Bottom Button */}
            <div className="p-3 border-t border-[#E2E8F0] bg-[#FAF8FF]">
              <button
                id="btn-view-full-queue"
                onClick={() => setShowFullQueueModal(true)}
                className="w-full h-8 border border-[#CBD5E1] text-[#0F172A] hover:text-[#006194] hover:bg-white text-[12px] font-medium rounded transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <span>{isAr ? 'عرض القائمة الكاملة' : 'View Full Queue'}</span>
              </button>
            </div>
          </aside>
        </div>
      </div>

      {/* Full Queue Details Modal */}
      {showFullQueueModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in">
          <div className="bg-white rounded-xl border border-[#CBD5E1] max-w-lg w-full p-6 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-[#E2E8F0] mb-4">
              <div>
                <h3 className="text-[18px] font-bold text-[#0F172A]">
                  {isAr ? 'قائمة المرضى في العيادة' : 'Live Patient Queue'}
                </h3>
                <p className="text-[12px] text-[#64748B]">
                  {isAr ? 'تتبع فوري لمراحل الكشف والانتظار' : 'Real-time clinic waiting list management'}
                </p>
              </div>
              <button
                onClick={() => setShowFullQueueModal(false)}
                className="p-1 text-[#64748B] hover:text-[#0F172A] rounded-md"
              >
                ✕
              </button>
            </div>

            <div className="space-y-3 max-h-[350px] overflow-y-auto pe-1">
              {queue.map((item) => (
                <div
                  key={item.id}
                  className="p-3 rounded-lg border border-[#E2E8F0] bg-[#F8FAFC] flex justify-between items-center"
                >
                  <div>
                    <h5 className="text-[13px] font-bold text-[#0F172A]">
                      {isAr ? item.patientName : item.patientNameEn}
                    </h5>
                    <p className="text-[11px] text-[#64748B]">
                      {item.time} • {item.room} • {isAr ? item.doctor : item.doctorEn}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span
                      className={`text-[11px] px-2 py-0.5 rounded font-medium ${
                        item.status === 'in_consultation'
                          ? 'bg-[#cce5ff] text-[#004b73]'
                          : item.status === 'completed'
                          ? 'bg-[#E2E8F0] text-[#64748B]'
                          : 'bg-[#ffdcc0] text-[#894d00]'
                      }`}
                    >
                      {item.status}
                    </span>
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-5 flex justify-end">
              <button
                onClick={() => setShowFullQueueModal(false)}
                className="px-4 py-2 bg-[#006194] text-white rounded text-[13px] font-medium"
              >
                {isAr ? 'إغلاق' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
