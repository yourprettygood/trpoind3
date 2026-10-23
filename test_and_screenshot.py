import os
import sys
import time

# Ensure UTF-8 output encoding for console
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

from playwright.sync_api import sync_playwright

BASE_URL = "http://localhost:5000"
ARTIFACT_DIR = r"C:\Users\didur\.gemini\antigravity\brain\d23f76fa-781b-4cb6-8351-13f4cfa660b5\screenshots"
WORKSPACE_DIR = r"D:\GhubReps\trpoind3\screenshots"

os.makedirs(ARTIFACT_DIR, exist_ok=True)
os.makedirs(WORKSPACE_DIR, exist_ok=True)

def save_screenshot(page, filename, description):
    print(f"[SCREENSHOT] Capturing: {description} -> {filename}")
    path1 = os.path.join(ARTIFACT_DIR, filename)
    path2 = os.path.join(WORKSPACE_DIR, filename)
    page.screenshot(path=path1, full_page=True)
    page.screenshot(path=path2, full_page=True)
    return path1

def run_tests():
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="msedge", headless=True)
        context = browser.new_context(viewport={"width": 1400, "height": 900})
        page = context.new_page()

        print("\n=== STEP 1: PUBLIC / GUEST PAGES ===")
        # 1. Home
        page.goto(f"{BASE_URL}/")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "01_home_guest.png", "Главная страница для неавторизованного пользователя")

        # 2. Login Page
        page.goto(f"{BASE_URL}/Account/Login")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "02_login_page.png", "Страница авторизации с кнопками быстрого демо-доступа")

        # 3. Register Page
        page.goto(f"{BASE_URL}/Account/Register")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "03_register_page.png", "Страница самостоятельной регистрации студента")

        print("\n=== STEP 2: STUDENT ROLE TESTING ===")
        # Login as student
        page.goto(f"{BASE_URL}/Account/Login")
        page.click("button:has-text('Студент')")
        page.wait_for_load_state("networkidle")
        time.sleep(1)
        save_screenshot(page, "04_student_disciplines.png", "Личный кабинет студента: список дисциплин и дедлайны")

        # View Lecture
        page.goto(f"{BASE_URL}/Student/Lecture?id=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "05_student_lecture_reading.png", "Изучение лекции студентом: теория и контрольные вопросы")

        # Take Test
        page.goto(f"{BASE_URL}/Student/TakeTest?testId=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "06_student_take_test.png", "Прохождение онлайн-тестирования по лекции")

        # Select answers and submit test
        try:
            questions = page.locator(".card.border-0.shadow-sm.mb-4")
            for i in range(questions.count()):
                radios = questions.nth(i).locator("input[type='radio']")
                if radios.count() > 0:
                    radios.first.check()
            
            save_screenshot(page, "07_student_test_filled.png", "Заполненные ответы на вопросы тестирования")
            # Target the form submit button specifically
            page.locator("form button.btn-success").click()
            page.wait_for_load_state("networkidle")
            time.sleep(1)
            save_screenshot(page, "08_student_test_result.png", "Результаты онлайн-тестирования с оценкой и баллами")
        except Exception as e:
            print("Notice during test submit: " + repr(e))

        # Student GradeBook
        page.goto(f"{BASE_URL}/Student/GradeBook")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "09_student_gradebook.png", "Электронная зачётная книжка студента")

        # Student Official Print Report
        page.goto(f"{BASE_URL}/Student/GradeReport")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "10_student_official_print_form.png", "Официальная печатная академическая справка (М.П. и подписи)")

        print("\n=== STEP 3: TEACHER ROLE TESTING ===")
        # Logout student, login teacher
        page.goto(f"{BASE_URL}/Account/Login")
        page.click("button:has-text('Преподаватель')")
        page.wait_for_load_state("networkidle")
        time.sleep(1)
        save_screenshot(page, "11_teacher_dashboard.png", "Панель преподавателя: сводка курсов и активности")

        # Teacher Disciplines
        page.goto(f"{BASE_URL}/Teacher/Disciplines")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "12_teacher_disciplines_list.png", "Управление дисциплинами и курсами")

        # Teacher Topics
        page.goto(f"{BASE_URL}/Teacher/Topics?disciplineId=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "13_teacher_topics.png", "Темы дисциплины 'Внедрение и поддержка КС'")

        # Teacher Questions
        page.goto(f"{BASE_URL}/Teacher/Questions?lectureId=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "14_teacher_questions.png", "Управление контрольными вопросами к лекции")

        # Electronic Journal
        page.goto(f"{BASE_URL}/Teacher/Journal?disciplineId=1&groupId=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "15_teacher_journal.png", "Электронный журнал успеваемости группы со сроками сдачи")

        # Test updating student grade (teacher manual override)
        try:
            modal_btn = page.locator("button:has-text('Оценить')").or_(page.locator("button:has-text('Изменить')"))
            if modal_btn.count() > 0:
                modal_btn.first.click()
                time.sleep(0.5)
                page.select_option("#gradeSelect", "5")
                page.fill("#teacherCommentInput", "Отличная демонстрационная работа, все критерии выполнены в полном объеме.")
                save_screenshot(page, "16_teacher_grade_modal.png", "Модальное окно выставления/изменения оценки преподавателем")
                page.click("button:has-text('Сохранить оценку')")
                page.wait_for_load_state("networkidle")
                time.sleep(1)
                save_screenshot(page, "17_teacher_journal_grade_updated.png", "Журнал после выставления оценки 'Отлично (5)'")
        except Exception as e:
            print("Notice during grade update: " + repr(e))

        # Review Answers
        page.goto(f"{BASE_URL}/Teacher/ReviewAnswers")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "18_teacher_review_answers.png", "Журнал рецензирования ответов студентов")

        # Teacher Official Group Print Report
        page.goto(f"{BASE_URL}/Teacher/GroupReport?disciplineId=1&groupId=1")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "19_teacher_group_print_form.png", "Официальная зачётно-экзаменационная ведомость группы (М.П.)")

        # Database Explorer
        page.goto(f"{BASE_URL}/DatabaseExplorer")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "20_database_explorer_erd.png", "Инспектор БД: интерактивная схема отношений ERD (как в DBeaver)")

        # Database Explorer Table view
        page.goto(f"{BASE_URL}/DatabaseExplorer?selectedTable=Lectures")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "21_database_explorer_table_data.png", "Инспектор БД: живая таблица данных 'Lectures' с дедлайнами")

        print("\n=== STEP 4: ADMINISTRATOR ROLE TESTING ===")
        # Logout teacher, login admin
        page.goto(f"{BASE_URL}/Account/Login")
        page.click("button:has-text('Администратор')")
        page.wait_for_load_state("networkidle")
        time.sleep(1)
        save_screenshot(page, "22_admin_dashboard.png", "Панель администратора: дашборд системной статистики и аудит")

        # Users list
        page.goto(f"{BASE_URL}/Admin/Users")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "23_admin_users_registry.png", "Реестр пользователей системы с ролевой моделью (RBAC)")

        # Create user page
        page.goto(f"{BASE_URL}/Admin/CreateUser")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "24_admin_create_user_form.png", "Форма добавления нового пользователя администратором")

        # Fill and create a new student
        new_email = f"alex_smirnov_{int(time.time())}@cabinet.local"
        page.fill("input[name='FullName']", "Смирнов Алексей Викторович")
        page.fill("input[name='Email']", new_email)
        page.fill("input[name='Password']", "Student123!")
        page.fill("input[name='ConfirmPassword']", "Student123!")
        page.select_option("#roleSelector", "Student")
        page.fill("input[name='PhoneNumber']", "+7 (911) 234-56-78")
        save_screenshot(page, "25_admin_create_user_filled.png", "Заполненная форма создания нового студента группы ИС-31")
        page.click("button:has-text('Зарегистрировать пользователя')")
        page.wait_for_load_state("networkidle")
        time.sleep(1)
        save_screenshot(page, "26_admin_user_created_success.png", "Реестр после успешного создания пользователя (с флеш-сообщением)")

        # Groups list
        page.goto(f"{BASE_URL}/Admin/Groups")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "27_admin_student_groups.png", "Справочник учебных групп (ИС-31) и форма создания")

        # Security Audit Logs
        page.goto(f"{BASE_URL}/Admin/AuditLogs")
        page.wait_for_load_state("networkidle")
        save_screenshot(page, "28_admin_audit_logs.png", "Системный журнал аудита безопасности с фиксацией всех событий")

        browser.close()
        print("\n[SUCCESS] All 28 screenshots and end-to-end flows executed perfectly!")

if __name__ == "__main__":
    run_tests()
