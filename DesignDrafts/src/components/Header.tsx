import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { NavTab } from '../types';
import {
  ChevronDown,
  Globe,
  Bell,
  Settings as SettingsIcon,
  Menu,
  X,
  LogOut,
  UserCheck,
  Shield,
  Stethoscope,
  HeartPulse,
} from 'lucide-react';

interface HeaderProps {
  onOpenMobileMenu?: () => void;
}

export const Header: React.FC<HeaderProps> = ({ onOpenMobileMenu }) => {
  const {
    lang,
    setLang,
    toggleLang,
    activeTab,
    setActiveTab,
    selectedClinic,
    setSelectedClinic,
    userRole,
    login,
    logout,
    addToast,
  } = useApp();

  const isAr = lang === 'ar';
  const [isClinicOpen, setIsClinicOpen] = useState(false);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [isNotifOpen, setIsNotifOpen] = useState(false);

  const getPageTitle = (tab: NavTab) => {
    switch (tab) {
      case 'dashboard':
        return isAr ? 'نظرة عامة على العيادة' : 'Clinic Overview';
      case 'patients':
        return isAr ? 'الملف الطبي وسجل المريض' : 'EMR & Patient Profile';
      case 'schedules':
        return isAr ? 'الجداول والمواعيد المباشرة' : 'Live Schedule & Appointments';
      case 'records':
        return isAr ? 'السجلات الطبية الرقمية' : 'Digital Medical Records';
      case 'inventory':
        return isAr ? 'إدارة المخزون الطبي' : 'Medical Inventory';
      case 'analytics':
        return isAr ? 'لوحة التحليلات والمؤشرات' : 'Analytics & Insights';
      case 'settings':
        return isAr ? 'مركز التحكم والإعدادات للمسؤول' : 'Admin Control & Settings Hub';
      case 'support':
        return isAr ? 'الدعم الفني والمساعدة' : 'Support & Help Center';
      default:
        return isAr ? 'المركز الطبي' : 'Medical Center';
    }
  };

  const clinics = [
    { ar: 'المركز الطبي المركزي', en: 'Central Medical' },
    { ar: 'فرع العيادات التخصصية', en: 'Specialty Clinics Branch' },
    { ar: 'مستشفى الرعاية النهارية', en: 'Daycare Medical Hospital' },
  ];

  return (
    <>
      {/* Mobile Header */}
      <header
        id="mobile-header"
        className="md:hidden w-full h-16 sticky top-0 z-50 bg-white border-b border-[#E2E8F0] flex justify-between items-center px-4 shrink-0 shadow-xs"
      >
        <div className="flex items-center gap-3">
          <button
            id="mobile-menu-btn"
            onClick={onOpenMobileMenu}
            className="p-1.5 rounded-md text-[#64748B] hover:text-[#0F172A] hover:bg-[#F8FAFC] active:scale-95 transition-all"
            aria-label="Toggle menu"
          >
            <Menu className="w-5 h-5" />
          </button>
          <span className="text-[17px] font-bold text-[#006194]">ClinicFlow</span>
        </div>

        <div className="flex items-center gap-2">
          {/* Quick Language Toggle */}
          <button
            id="mobile-lang-btn"
            onClick={toggleLang}
            className="text-[11px] font-semibold text-[#006194] bg-[#eaedff] px-2 py-1 rounded border border-[#dae2fd]"
          >
            {isAr ? 'English' : 'عربي'}
          </button>

          <button
            onClick={() => setActiveTab('settings')}
            className="p-1 text-[#64748B] hover:text-[#006194]"
          >
            <SettingsIcon className="w-5 h-5" />
          </button>

          <div
            onClick={() => setIsProfileOpen(!isProfileOpen)}
            className="w-8 h-8 rounded-full overflow-hidden border border-[#E2E8F0] cursor-pointer"
          >
            <img
              src="https://lh3.googleusercontent.com/aida-public/AB6AXuBUqukMRmXvfQmYbVotRKYNwH8gZ5L6-aC-v31A_zsW7YqlujexiZ6hsLhlN3IraCLigQ1t-GnkZGpatNQmG0HtDwTuVj2uf1ZAaaxcWVAj2C5y1SZ0SehrG4ZnDZt7ClyqvWmEiIxsru5UFue8Fojm8DNRicg0pi5TUCI5GV22lAvXMqvQ6QZgVlynl3NK9c3bFYPbbs40PkKztujRK3m-kiqhJ4RjFcqRDmG9fMs4J3KbOA4jSFLZ"
              alt="Admin avatar"
              className="w-full h-full object-cover"
            />
          </div>
        </div>
      </header>

      {/* Desktop Header */}
      <header
        id="desktop-header"
        className="hidden md:flex w-full h-16 shrink-0 bg-white border-b border-[#E2E8F0] items-center justify-between px-6 sticky top-0 z-30 shadow-2xs"
      >
        <div className="flex items-center gap-4">
          <h2 className="text-[20px] font-bold text-[#0F172A] tracking-tight">
            {getPageTitle(activeTab)}
          </h2>
          <div className="h-5 w-px bg-[#CBD5E1]" />

          {/* Clinic Switcher */}
          <div className="relative">
            <button
              id="btn-clinic-switcher"
              onClick={() => setIsClinicOpen(!isClinicOpen)}
              className="flex items-center gap-1.5 px-2.5 py-1 rounded text-[#64748B] hover:text-[#006194] hover:bg-[#F8FAFC] text-[13px] font-medium transition-colors cursor-pointer"
            >
              <span>{selectedClinic}</span>
              <ChevronDown className="w-3.5 h-3.5" />
            </button>

            {isClinicOpen && (
              <div
                className="absolute top-full mt-1.5 w-56 bg-white border border-[#CBD5E1] rounded-lg shadow-lg py-1 z-50 animate-in fade-in slide-in-from-top-1"
                style={{ [isAr ? 'right' : 'left']: 0 }}
              >
                {clinics.map((c, i) => (
                  <button
                    key={i}
                    onClick={() => {
                      setSelectedClinic(isAr ? c.ar : c.en);
                      setIsClinicOpen(false);
                      addToast(
                        isAr ? 'تم تبديل العيادة' : 'Clinic Switched',
                        isAr ? `أنت الآن في ${c.ar}` : `Switched to ${c.en}`,
                        'info'
                      );
                    }}
                    className="w-full text-start px-3 py-2 text-[13px] text-[#0F172A] hover:bg-[#eaedff] hover:text-[#006194] transition-colors"
                  >
                    {isAr ? c.ar : c.en}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Right Tools Area */}
        <div className="flex items-center gap-3">
          {/* Language Switcher Pill */}
          <div
            id="lang-switcher"
            className="flex items-center bg-[#F8FAFC] rounded-md p-0.5 border border-[#E2E8F0]"
          >
            <button
              onClick={() => setLang('ar')}
              className={`px-2.5 py-1 rounded text-[12px] font-medium transition-all ${
                isAr
                  ? 'bg-white text-[#006194] shadow-xs font-bold'
                  : 'text-[#64748B] hover:text-[#0F172A]'
              }`}
            >
              العربية
            </button>
            <button
              onClick={() => setLang('en')}
              className={`px-2.5 py-1 rounded text-[12px] font-medium transition-all ${
                !isAr
                  ? 'bg-white text-[#006194] shadow-xs font-bold'
                  : 'text-[#64748B] hover:text-[#0F172A]'
              }`}
            >
              EN
            </button>
          </div>

          {/* Notifications */}
          <div className="relative">
            <button
              id="btn-notifications"
              onClick={() => setIsNotifOpen(!isNotifOpen)}
              className="w-8 h-8 flex items-center justify-center rounded-md text-[#64748B] hover:text-[#006194] hover:bg-[#F8FAFC] transition-colors relative cursor-pointer"
              title={isAr ? 'الإشعارات' : 'Notifications'}
            >
              <Bell className="w-4 h-4" />
              <span className="absolute top-1.5 end-1.5 w-2 h-2 bg-[#ba1a1a] rounded-full" />
            </button>

            {isNotifOpen && (
              <div
                className="absolute top-full mt-2 w-80 bg-white border border-[#CBD5E1] rounded-lg shadow-xl p-3 z-50"
                style={{ [isAr ? 'left' : 'right']: 0 }}
              >
                <div className="flex justify-between items-center pb-2 border-b border-[#E2E8F0] mb-2">
                  <h4 className="text-[13px] font-bold text-[#0F172A]">
                    {isAr ? 'التنبيهات السريرية' : 'Clinical Alerts'}
                  </h4>
                  <span className="text-[11px] text-[#006194] font-medium">3 {isAr ? 'جديد' : 'new'}</span>
                </div>
                <div className="space-y-2 text-[12px]">
                  <div className="p-2 bg-[#DCFCE7]/60 rounded border border-[#DCFCE7] text-[#166534]">
                    <strong>{isAr ? 'اكتمال المزامنة' : 'Cloud Sync Complete'}</strong>
                    <p className="text-[11px] opacity-80">{isAr ? 'تم نسخ بيانات المرضى إلى Google Drive' : 'Patient data snapshot backed up'}</p>
                  </div>
                  <div className="p-2 bg-[#eaedff] rounded border border-[#dae2fd] text-[#006194]">
                    <strong>{isAr ? 'المريض جون دو' : 'Patient John Doe'}</strong>
                    <p className="text-[11px] opacity-80">{isAr ? 'بدأت استشارة القلب في الغرفة A' : 'In consultation with Dr. Adams'}</p>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Quick Settings Icon */}
          <button
            id="btn-header-settings"
            onClick={() => setActiveTab('settings')}
            className={`w-8 h-8 flex items-center justify-center rounded-md transition-colors cursor-pointer ${
              activeTab === 'settings'
                ? 'text-[#006194] bg-[#eaedff]'
                : 'text-[#64748B] hover:text-[#006194] hover:bg-[#F8FAFC]'
            }`}
            title={isAr ? 'الإعدادات' : 'Settings'}
          >
            <SettingsIcon className="w-4 h-4" />
          </button>

          <div className="h-5 w-px bg-[#E2E8F0]" />

          {/* User Profile Avatar with dropdown */}
          <div className="relative">
            <div
              id="user-profile-menu"
              onClick={() => setIsProfileOpen(!isProfileOpen)}
              className="flex items-center gap-2 cursor-pointer p-1 rounded-md hover:bg-[#F8FAFC] transition-colors"
            >
              <img
                src="https://lh3.googleusercontent.com/aida-public/AB6AXuBUqukMRmXvfQmYbVotRKYNwH8gZ5L6-aC-v31A_zsW7YqlujexiZ6hsLhlN3IraCLigQ1t-GnkZGpatNQmG0HtDwTuVj2uf1ZAaaxcWVAj2C5y1SZ0SehrG4ZnDZt7ClyqvWmEiIxsru5UFue8Fojm8DNRicg0pi5TUCI5GV22lAvXMqvQ6QZgVlynl3NK9c3bFYPbbs40PkKztujRK3m-kiqhJ4RjFcqRDmG9fMs4J3KbOA4jSFLZ"
                alt="Admin avatar"
                className="w-8 h-8 rounded-full border border-[#CBD5E1] object-cover"
              />
              <div className="hidden lg:block text-start leading-tight">
                <p className="text-[12px] font-bold text-[#0F172A]">
                  {isAr ? 'د. سارة آدمن' : 'Dr. Sarah Admin'}
                </p>
                <span className="text-[10px] text-[#64748B] uppercase">
                  {userRole.toUpperCase()}
                </span>
              </div>
              <ChevronDown className="w-3.5 h-3.5 text-[#64748B]" />
            </div>

            {isProfileOpen && (
              <div
                className="absolute top-full mt-2 w-56 bg-white border border-[#CBD5E1] rounded-lg shadow-xl py-1.5 z-50 animate-in fade-in"
                style={{ [isAr ? 'left' : 'right']: 0 }}
              >
                <div className="px-3 py-2 border-b border-[#E2E8F0] mb-1">
                  <p className="text-[12px] font-bold text-[#0F172A]">
                    {isAr ? 'سارة آدمن' : 'Sarah Admin'}
                  </p>
                  <p className="text-[11px] text-[#64748B]">sarah@clinicflow.com</p>
                </div>

                <div className="px-3 py-1 text-[11px] font-semibold text-[#64748B] uppercase">
                  {isAr ? 'تبديل الدور' : 'Switch Role'}
                </div>
                <button
                  onClick={() => {
                    login('admin');
                    setIsProfileOpen(false);
                  }}
                  className={`w-full px-3 py-1.5 text-[12px] flex items-center gap-2 text-start ${
                    userRole === 'admin' ? 'bg-[#eaedff] text-[#006194] font-bold' : 'text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  <Shield className="w-3.5 h-3.5" />
                  <span>{isAr ? 'مسؤول (Admin)' : 'Admin'}</span>
                </button>
                <button
                  onClick={() => {
                    login('doctor');
                    setIsProfileOpen(false);
                  }}
                  className={`w-full px-3 py-1.5 text-[12px] flex items-center gap-2 text-start ${
                    userRole === 'doctor' ? 'bg-[#eaedff] text-[#006194] font-bold' : 'text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  <Stethoscope className="w-3.5 h-3.5" />
                  <span>{isAr ? 'طبيب (Doctor)' : 'Doctor'}</span>
                </button>
                <button
                  onClick={() => {
                    login('nurse');
                    setIsProfileOpen(false);
                  }}
                  className={`w-full px-3 py-1.5 text-[12px] flex items-center gap-2 text-start ${
                    userRole === 'nurse' ? 'bg-[#eaedff] text-[#006194] font-bold' : 'text-[#0F172A] hover:bg-[#F8FAFC]'
                  }`}
                >
                  <HeartPulse className="w-3.5 h-3.5" />
                  <span>{isAr ? 'ممرض (Nurse)' : 'Nurse'}</span>
                </button>

                <div className="border-t border-[#E2E8F0] my-1" />
                <button
                  id="btn-logout"
                  onClick={() => {
                    setIsProfileOpen(false);
                    logout();
                  }}
                  className="w-full px-3 py-2 text-[12px] text-[#ba1a1a] hover:bg-[#FEE2E2] flex items-center gap-2 text-start transition-colors"
                >
                  <LogOut className="w-3.5 h-3.5" />
                  <span>{isAr ? 'تسجيل الخروج' : 'Log Out'}</span>
                </button>
              </div>
            )}
          </div>
        </div>
      </header>
    </>
  );
};
