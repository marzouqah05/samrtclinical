import React, { useState } from 'react';
import { AppProvider, useApp } from './context/AppContext';
import { Sidebar } from './components/Sidebar';
import { Header } from './components/Header';
import { AuthScreen } from './components/AuthScreen';
import { DashboardView } from './components/DashboardView';
import { PatientProfileView } from './components/PatientProfileView';
import { AdminSettingsView } from './components/AdminSettingsView';
import { AppointmentsModal } from './components/AppointmentsModal';
import { AddUserModal } from './components/AddUserModal';
import { FileViewerModal } from './components/FileViewerModal';
import { Toast } from './components/Toast';
import {
  LayoutDashboard,
  Users,
  Calendar,
  FileText,
  Package,
  TrendingUp,
  Settings,
  HelpCircle,
  X,
  Plus,
} from 'lucide-react';
import { NavTab } from './types';

const MainLayout: React.FC = () => {
  const {
    isAuthenticated,
    activeTab,
    setActiveTab,
    lang,
    setIsApptModalOpen,
  } = useApp();

  const isAr = lang === 'ar';
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  if (!isAuthenticated) {
    return (
      <>
        <AuthScreen />
        <Toast />
      </>
    );
  }

  const mobileNavItems: { id: NavTab; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
    { id: 'dashboard', label: isAr ? 'لوحة التحكم' : 'Dashboard', icon: LayoutDashboard },
    { id: 'patients', label: isAr ? 'المرضى' : 'Patients', icon: Users },
    { id: 'schedules', label: isAr ? 'الجداول' : 'Schedules', icon: Calendar },
    { id: 'records', label: isAr ? 'السجلات الطبية' : 'Medical Records', icon: FileText },
    { id: 'inventory', label: isAr ? 'المخزون' : 'Inventory', icon: Package },
    { id: 'analytics', label: isAr ? 'التحليلات' : 'Analytics', icon: TrendingUp },
    { id: 'settings', label: isAr ? 'الإعدادات' : 'Settings', icon: Settings },
    { id: 'support', label: isAr ? 'الدعم' : 'Support', icon: HelpCircle },
  ];

  return (
    <div className="flex h-screen w-full bg-[#F8FAFC] overflow-hidden text-[#0F172A]">
      {/* Desktop Sidebar */}
      <Sidebar />

      {/* Mobile Drawer */}
      {mobileMenuOpen && (
        <div className="fixed inset-0 z-50 md:hidden flex">
          <div
            className="fixed inset-0 bg-black/40 backdrop-blur-xs"
            onClick={() => setMobileMenuOpen(false)}
          />
          <div
            className={`relative bg-white w-64 h-full p-4 flex flex-col z-10 shadow-2xl transition-transform ${
              isAr ? 'mr-auto' : 'ml-auto'
            }`}
          >
            <div className="flex items-center justify-between pb-3 border-b border-[#E2E8F0] mb-3">
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-[24px] text-[#006194]">
                  local_hospital
                </span>
                <span className="text-[17px] font-bold text-[#006194]">ClinicFlow</span>
              </div>
              <button
                onClick={() => setMobileMenuOpen(false)}
                className="p-1 text-[#64748B] hover:text-[#0F172A] rounded-md"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <button
              onClick={() => {
                setMobileMenuOpen(false);
                setIsApptModalOpen(true);
              }}
              className="w-full h-10 bg-[#006194] text-white font-medium text-[13px] rounded-md mb-4 flex items-center justify-center gap-1.5"
            >
              <Plus className="w-4 h-4" />
              <span>{isAr ? 'موعد جديد' : 'New Appointment'}</span>
            </button>

            <div className="flex-1 space-y-1 overflow-y-auto">
              {mobileNavItems.map((item) => {
                const Icon = item.icon;
                const isActive = activeTab === item.id;
                return (
                  <button
                    key={item.id}
                    onClick={() => {
                      setActiveTab(item.id);
                      setMobileMenuOpen(false);
                    }}
                    className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-[13px] font-medium text-start ${
                      isActive
                        ? 'bg-[#eaedff] text-[#006194] font-bold'
                        : 'text-[#64748B] hover:bg-[#F8FAFC]'
                    }`}
                  >
                    <Icon className={`w-4 h-4 ${isActive ? 'text-[#006194]' : 'text-[#64748B]'}`} />
                    <span>{item.label}</span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* Main Content Viewport */}
      <div className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
        <Header onOpenMobileMenu={() => setMobileMenuOpen(true)} />

        <main className="flex-1 flex flex-col min-h-0 overflow-hidden">
          {activeTab === 'dashboard' && <DashboardView />}
          {activeTab === 'patients' && <PatientProfileView />}
          {activeTab === 'schedules' && <DashboardView />}
          {activeTab === 'records' && <PatientProfileView />}
          {activeTab === 'settings' && <AdminSettingsView />}

          {(activeTab === 'inventory' || activeTab === 'analytics' || activeTab === 'support') && (
            <div className="flex-1 p-8 overflow-y-auto flex flex-col items-center justify-center text-center">
              <div className="max-w-md bg-white p-8 rounded-2xl border border-[#E2E8F0] shadow-xs">
                <div className="w-14 h-14 rounded-full bg-[#eaedff] text-[#006194] flex items-center justify-center mx-auto mb-4">
                  {activeTab === 'inventory' && <Package className="w-7 h-7" />}
                  {activeTab === 'analytics' && <TrendingUp className="w-7 h-7" />}
                  {activeTab === 'support' && <HelpCircle className="w-7 h-7" />}
                </div>
                <h3 className="text-[20px] font-bold text-[#0F172A] mb-2">
                  {activeTab === 'inventory'
                    ? isAr
                      ? 'وحدة إدارة المخزون والأدوية'
                      : 'Medical Inventory & Pharmacy'
                    : activeTab === 'analytics'
                    ? isAr
                      ? 'التحليلات والمؤشرات الإحصائية'
                      : 'Advanced Clinic Analytics'
                    : isAr
                    ? 'مركز الدعم الفني والمساعدة'
                    : 'ClinicFlow Help Center'}
                </h3>
                <p className="text-[13px] text-[#64748B] mb-6">
                  {isAr
                    ? 'هذا القسم مرتبط بالنظام المباشر ومحدث بأحدث معايير الأمان والتشغيل.'
                    : 'This module is synchronized with real-time clinic operations and access controls.'}
                </p>
                <button
                  onClick={() => setActiveTab('dashboard')}
                  className="px-4 py-2 bg-[#006194] text-white text-[13px] font-medium rounded-lg hover:bg-[#004b73] transition-colors"
                >
                  {isAr ? 'العودة للوحة التحكم' : 'Back to Dashboard'}
                </button>
              </div>
            </div>
          )}
        </main>
      </div>

      {/* Global Modals & Notifications */}
      <AppointmentsModal />
      <AddUserModal />
      <FileViewerModal />
      <Toast />
    </div>
  );
};

export default function App() {
  return (
    <AppProvider>
      <MainLayout />
    </AppProvider>
  );
}
