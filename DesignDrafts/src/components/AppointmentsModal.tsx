import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { X, Calendar, Clock, User, Stethoscope, DoorClosed, Send } from 'lucide-react';

export const AppointmentsModal: React.FC = () => {
  const { isApptModalOpen, setIsApptModalOpen, addAppointment, lang, patient, updatePatient } = useApp();
  const isAr = lang === 'ar';

  const [patientName, setPatientName] = useState('');
  const [telegramChatId, setTelegramChatId] = useState('');
  const [apptType, setApptType] = useState('general');
  const [doctor, setDoctor] = useState('Dr. Smith');
  const [timeSlot, setTimeSlot] = useState('02:00 PM');
  const [room, setRoom] = useState('غرفة A');

  if (!isApptModalOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!patientName.trim()) return;

    const doctorAr =
      doctor === 'Dr. Smith'
        ? 'د. سميث'
        : doctor === 'Dr. Adams'
        ? 'د. آدامز'
        : doctor === 'Dr. Sarah Jenkins'
        ? 'د. سارة جنكينز'
        : 'د. جيمس جونز';

    const typeAr =
      apptType === 'general'
        ? `فحص عام - ${doctorAr}`
        : apptType === 'cardiology'
        ? `استشارة قلب - ${doctorAr}`
        : apptType === 'pediatrics'
        ? `كشف أطفال - ${doctorAr}`
        : `متابعة دورية - ${doctorAr}`;

    const typeEn =
      apptType === 'general'
        ? `General Checkup - ${doctor}`
        : apptType === 'cardiology'
        ? `Cardiology Consult - ${doctor}`
        : apptType === 'pediatrics'
        ? `Pediatrics - ${doctor}`
        : `Follow-up - ${doctor}`;

    addAppointment({
      patientName: patientName,
      patientNameEn: patientName,
      type: typeAr,
      typeEn: typeEn,
      doctor: doctorAr,
      doctorEn: doctor,
      timeSlot: timeSlot,
      status: 'scheduled',
      room: isAr ? room : room === 'غرفة A' ? 'Room A' : room === 'غرفة B' ? 'Room B' : 'Room C',
      telegramChatId: telegramChatId.trim() || undefined,
    });

    if (telegramChatId.trim() && patient) {
      updatePatient({ telegramChatId: telegramChatId.trim() });
    }

    setIsApptModalOpen(false);
    setPatientName('');
    setTelegramChatId('');
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in">
      <div className="bg-white rounded-xl border border-[#CBD5E1] max-w-md w-full p-6 shadow-2xl relative">
        <div className="flex justify-between items-center pb-3 border-b border-[#E2E8F0] mb-4">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded bg-[#eaedff] text-[#006194] flex items-center justify-center">
              <Calendar className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-[17px] font-bold text-[#0F172A]">
                {isAr ? 'حجز موعد سريري جديد' : 'Book New Clinical Appointment'}
              </h3>
              <p className="text-[11px] text-[#64748B]">
                {isAr ? 'تسجيل الموعد وإدراجه في جدول اليوم' : 'Add slot to live schedule & patient queue'}
              </p>
            </div>
          </div>
          <button
            onClick={() => setIsApptModalOpen(false)}
            className="text-[#64748B] hover:text-[#0F172A] p-1 rounded-md cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Patient Name */}
          <div>
            <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
              {isAr ? 'اسم المريض' : 'Patient Name'}
            </label>
            <div className="relative">
              <input
                type="text"
                required
                value={patientName}
                onChange={(e) => setPatientName(e.target.value)}
                placeholder={isAr ? 'مثال: محمد العمري' : 'e.g., Michael Brown'}
                className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[13px] text-[#0F172A] focus:outline-none focus:border-[#006194]"
              />
            </div>
          </div>

          {/* Telegram Chat ID (n8n Integration) */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="block text-[12px] font-medium text-[#0F172A] flex items-center gap-1.5">
                <Send className="w-3.5 h-3.5 text-[#0088cc]" />
                <span>{isAr ? 'معرف محادثة تيليجرام (Telegram Chat ID)' : 'Telegram Chat ID'}</span>
              </label>
              <span className="text-[10px] text-[#0088cc] font-semibold bg-[#e0f2fe] px-1.5 py-0.5 rounded border border-[#bae6fd]">
                n8n Automation
              </span>
            </div>
            <div className="relative">
              <input
                type="text"
                value={telegramChatId}
                onChange={(e) => setTelegramChatId(e.target.value)}
                placeholder={isAr ? 'مثال: 849201842 أو معرف تيليجرام' : 'e.g., 849201842'}
                className="w-full bg-white border border-[#CBD5E1] rounded px-3 py-2 text-[13px] text-[#0F172A] focus:outline-none focus:border-[#0088cc]"
                dir="ltr"
              />
            </div>
            <p className="text-[11px] text-[#64748B] mt-1">
              {isAr
                ? 'يستخدم لإرسال التذكيرات والمواعيد تلقائياً عبر بوت التيليجرام وسير عمل n8n'
                : 'Used by n8n automated workflow to send real-time appointment reminders via Telegram'}
            </p>
          </div>

          {/* Doctor & Type */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'الطبيب المعالج' : 'Attending Doctor'}
              </label>
              <select
                value={doctor}
                onChange={(e) => setDoctor(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
              >
                <option value="Dr. Smith">{isAr ? 'د. سميث (عام)' : 'Dr. Smith (GP)'}</option>
                <option value="Dr. Adams">{isAr ? 'د. آدامز (قلب)' : 'Dr. Adams (Cardiology)'}</option>
                <option value="Dr. Sarah Jenkins">{isAr ? 'د. سارة جنكينز (استشاري)' : 'Dr. Sarah Jenkins'}</option>
                <option value="Dr. James Jones">{isAr ? 'د. جيمس جونز (أطفال)' : 'Dr. James Jones'}</option>
              </select>
            </div>

            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'نوع الكشف' : 'Visit Type'}
              </label>
              <select
                value={apptType}
                onChange={(e) => setApptType(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
              >
                <option value="general">{isAr ? 'فحص سريري عام' : 'General Checkup'}</option>
                <option value="cardiology">{isAr ? 'استشارة أمراض قلب' : 'Cardiology Consult'}</option>
                <option value="pediatrics">{isAr ? 'طب الأطفال' : 'Pediatrics'}</option>
                <option value="followup">{isAr ? 'متابعة دورية' : 'Follow-up'}</option>
              </select>
            </div>
          </div>

          {/* Time slot & Room */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'الوقت' : 'Time Slot'}
              </label>
              <select
                value={timeSlot}
                onChange={(e) => setTimeSlot(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
                dir="ltr"
              >
                <option value="09:00 AM">09:00 AM</option>
                <option value="10:00 AM">10:00 AM</option>
                <option value="11:00 AM">11:00 AM</option>
                <option value="01:00 PM">01:00 PM</option>
                <option value="02:00 PM">02:00 PM</option>
                <option value="03:00 PM">03:00 PM</option>
                <option value="04:00 PM">04:00 PM</option>
              </select>
            </div>

            <div>
              <label className="block text-[12px] font-medium text-[#0F172A] mb-1">
                {isAr ? 'الغرفة' : 'Room'}
              </label>
              <select
                value={room}
                onChange={(e) => setRoom(e.target.value)}
                className="w-full bg-white border border-[#CBD5E1] rounded px-2.5 py-2 text-[13px] text-[#0F172A]"
              >
                <option value="غرفة A">{isAr ? 'غرفة A' : 'Room A'}</option>
                <option value="غرفة B">{isAr ? 'غرفة B' : 'Room B'}</option>
                <option value="غرفة C">{isAr ? 'غرفة C' : 'Room C'}</option>
              </select>
            </div>
          </div>

          {/* Buttons */}
          <div className="pt-3 flex justify-end gap-2 border-t border-[#E2E8F0]">
            <button
              type="button"
              onClick={() => setIsApptModalOpen(false)}
              className="px-4 py-2 text-[13px] text-[#64748B] hover:text-[#0F172A] font-medium rounded cursor-pointer"
            >
              {isAr ? 'إلغاء' : 'Cancel'}
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-[#006194] hover:bg-[#004b73] text-white text-[13px] font-medium rounded transition-all cursor-pointer shadow-xs"
            >
              {isAr ? 'تأكيد الحجز' : 'Confirm Appointment'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
