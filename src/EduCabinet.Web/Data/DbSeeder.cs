using EduCabinet.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database exists
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roles = ["Administrator", "Teacher", "Student"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Users
        var adminUser = await userManager.FindByEmailAsync("admin@cabinet.local");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin@cabinet.local",
                Email = "admin@cabinet.local",
                FullName = "Администратор Системы",
                UserRole = "Administrator",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRoleAsync(adminUser, "Administrator");
        }

        var teacherUser = await userManager.FindByEmailAsync("teacher@cabinet.local");
        if (teacherUser == null)
        {
            teacherUser = new ApplicationUser
            {
                UserName = "teacher@cabinet.local",
                Email = "teacher@cabinet.local",
                FullName = "Иванов Сергей Павлович (Преподаватель)",
                UserRole = "Teacher",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(teacherUser, "Teacher123!");
            await userManager.AddToRoleAsync(teacherUser, "Teacher");
        }

        var student1 = await userManager.FindByEmailAsync("student@cabinet.local");
        if (student1 == null)
        {
            student1 = new ApplicationUser
            {
                UserName = "student@cabinet.local",
                Email = "student@cabinet.local",
                FullName = "Алексеев Даниил Игоревич (Студент)",
                UserRole = "Student",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(student1, "Student123!");
            await userManager.AddToRoleAsync(student1, "Student");
        }

        var student2 = await userManager.FindByEmailAsync("student2@cabinet.local");
        if (student2 == null)
        {
            student2 = new ApplicationUser
            {
                UserName = "student2@cabinet.local",
                Email = "student2@cabinet.local",
                FullName = "Смирнова Екатерина Олеговна (Студентка)",
                UserRole = "Student",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(student2, "Student123!");
            await userManager.AddToRoleAsync(student2, "Student");
        }

        // 3. Seed Group
        var group = await context.StudentGroups.FirstOrDefaultAsync(g => g.Name == "ИС-31");
        if (group == null)
        {
            group = new StudentGroup
            {
                Name = "ИС-31",
                CourseNumber = 3,
                Description = "Специальность «Информационные системы и программирование», 3 курс"
            };
            context.StudentGroups.Add(group);
            await context.SaveChangesAsync();

            // Link students to group
            context.GroupMemberships.Add(new GroupMembership { StudentId = student1.Id, GroupId = group.Id });
            context.GroupMemberships.Add(new GroupMembership { StudentId = student2.Id, GroupId = group.Id });
            await context.SaveChangesAsync();
        }

        // 4. Seed Disciplines, Topics, Lectures, Questions and Tests
        if (!await context.Disciplines.AnyAsync())
        {
            var discipline = new Discipline
            {
                Title = "Внедрение и поддержка компьютерных систем",
                Description = "Изучение жизненного цикла внедрения ПО, настройки рабочих мест, Kestrel/IIS серверов, регламентов бэкапа, контроля качества и поэтапного освоения материала.",
                TeacherId = teacherUser.Id,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Disciplines.Add(discipline);
            await context.SaveChangesAsync();

            // === TOPIC 1 ===
            var topic1 = new Topic
            {
                DisciplineId = discipline.Id,
                Title = "Тема 1. Процессы внедрения и настройка рабочей среды",
                Description = "Основы внедрения программного обеспечения, аппаратные требования и конфигурирование сервера.",
                OrderIndex = 1
            };
            context.Topics.Add(topic1);
            await context.SaveChangesAsync();

            // Lecture 1
            var lecture1 = new Lecture
            {
                TopicId = topic1.Id,
                Title = "Лекция 1. Жизненный цикл внедрения и требования к среде развёртывания",
                Description = "Рассмотрение стадий внедрения, подготовки серверной среды, настройки СУБД и прав доступа.",
                OrderIndex = 1,
                RequiresTestToUnlockNext = true,
                PassingScorePercent = 70,
                RequiresQuestionsCheck = false,
                Deadline = DateTime.UtcNow.AddDays(7),
                ContentHtml = @"
                    <h4>1. Понятие жизненного цикла внедрения</h4>
                    <p>Внедрение программного обеспечения — это комплекс организационно-технических мероприятий по развёртыванию разработанного продукта на площадке заказчика или в учебной лаборатории.</p>
                    <div class='alert alert-info'>
                        <strong>Ключевые этапы внедрения:</strong>
                        <ol class='mb-0'>
                            <li>Анализ готовности инфраструктуры (ОС, порты, СУБД).</li>
                            <li>Установка сред выполнения (.NET SDK / Runtime, PostgreSQL Server).</li>
                            <li>Развёртывание Release-сборки веб-приложения.</li>
                            <li>Инициализация базы данных и проведение миграций.</li>
                            <li>Приёмочное тестирование и обучение пользователей.</li>
                        </ol>
                    </div>
                    <h4>2. Разграничение доступа</h4>
                    <p>Для предотвращения несанкционированного доступа разграничение ролей (Преподаватель, Студент, Администратор) настраивается на уровне ядра ASP.NET Core Identity. Пользователи получают доступ строго к своему функционалу.</p>
                "
            };
            context.Lectures.Add(lecture1);
            await context.SaveChangesAsync();

            // Questions for Lecture 1
            var q1_1 = new ControlQuestion
            {
                LectureId = lecture1.Id,
                QuestionText = "Какой серверный компонент .NET используется в качестве кроссплатформенного веб-сервера по умолчанию?",
                QuestionType = QuestionType.ShortAnswer,
                CorrectAnswer = "Kestrel",
                MaxScore = 5,
                OrderIndex = 1
            };
            var q1_2 = new ControlQuestion
            {
                LectureId = lecture1.Id,
                QuestionText = "В какой среде компиляции следует собирать проект для ввода в эксплуатацию?",
                QuestionType = QuestionType.SingleChoice,
                CorrectAnswer = "Release",
                MaxScore = 5,
                OrderIndex = 2
            };
            context.ControlQuestions.AddRange(q1_1, q1_2);
            await context.SaveChangesAsync();

            // Test for Lecture 1
            var test1 = new Test
            {
                LectureId = lecture1.Id,
                Title = "Тест по лекции 1: Основы внедрения и рабочая среда",
                PassingScorePercent = 70,
                MaxAttempts = 3,
                TimeLimitMinutes = 15,
                ShuffleQuestions = false,
                ShuffleOptions = false
            };
            context.Tests.Add(test1);
            await context.SaveChangesAsync();

            var t1_q1 = new TestQuestion
            {
                TestId = test1.Id,
                QuestionText = "Что является главной целью процесса внедрения ПО на компьютер преподавателя?",
                QuestionType = QuestionType.SingleChoice,
                Points = 1,
                OrderIndex = 1
            };
            context.TestQuestions.Add(t1_q1);
            await context.SaveChangesAsync();

            context.AnswerOptions.AddRange(
                new AnswerOption { QuestionId = t1_q1.Id, OptionText = "Ввод программного продукта в рабочую эксплуатацию и настройка доступа", IsCorrect = true, OrderIndex = 1 },
                new AnswerOption { QuestionId = t1_q1.Id, OptionText = "Написание черновика исходного кода", IsCorrect = false, OrderIndex = 2 },
                new AnswerOption { QuestionId = t1_q1.Id, OptionText = "Удаление старой базы данных без сохранения резервных копий", IsCorrect = false, OrderIndex = 3 }
            );

            var t1_q2 = new TestQuestion
            {
                TestId = test1.Id,
                QuestionText = "Какой протокол обеспечивает безопасное шифрованное сетевое соединение со студентами?",
                QuestionType = QuestionType.SingleChoice,
                Points = 1,
                OrderIndex = 2
            };
            context.TestQuestions.Add(t1_q2);
            await context.SaveChangesAsync();

            context.AnswerOptions.AddRange(
                new AnswerOption { QuestionId = t1_q2.Id, OptionText = "HTTPS", IsCorrect = true, OrderIndex = 1 },
                new AnswerOption { QuestionId = t1_q2.Id, OptionText = "HTTP без шифрования", IsCorrect = false, OrderIndex = 2 },
                new AnswerOption { QuestionId = t1_q2.Id, OptionText = "FTP", IsCorrect = false, OrderIndex = 3 }
            );

            var t1_q3 = new TestQuestion
            {
                TestId = test1.Id,
                QuestionText = "Где должна выполняться проверка прав доступа к закрытым лекциям курса?",
                QuestionType = QuestionType.SingleChoice,
                Points = 1,
                OrderIndex = 3
            };
            context.TestQuestions.Add(t1_q3);
            await context.SaveChangesAsync();

            context.AnswerOptions.AddRange(
                new AnswerOption { QuestionId = t1_q3.Id, OptionText = "Строго на сервере (бэкенде) с защитой от прямого ввода URL", IsCorrect = true, OrderIndex = 1 },
                new AnswerOption { QuestionId = t1_q3.Id, OptionText = "Только скрытием ссылки в HTML интерфейса", IsCorrect = false, OrderIndex = 2 },
                new AnswerOption { QuestionId = t1_q3.Id, OptionText = "В браузере пользователя через cookie без серверной валидации", IsCorrect = false, OrderIndex = 3 }
            );

            await context.SaveChangesAsync();

            // Lecture 2
            var lecture2 = new Lecture
            {
                TopicId = topic1.Id,
                Title = "Лекция 2. Развёртывание веб-приложений и конфигурация Kestrel",
                Description = "Публикация проекта, настройка локального сетевого взаимодействия и брандмауэра.",
                OrderIndex = 2,
                RequiresTestToUnlockNext = true,
                PassingScorePercent = 70,
                RequiresQuestionsCheck = false,
                Deadline = DateTime.UtcNow.AddDays(14),
                ContentHtml = @"
                    <h4>1. Публикация в Release-конфигурации</h4>
                    <p>Команда <code>dotnet publish -c Release</code> собирает оптимизированный код без символов отладки, готовый к запуску через Kestrel.</p>
                    <h4>2. Сетевая изоляция</h4>
                    <p>Для предоставления доступа студентам в локальной сети настраивается привязка веб-сервера к IP-адресу машины преподавателя или порту 5000/5001. СУБД PostgreSQL не открывается во внешнюю сеть напрямую, обеспечивая безопасность данных.</p>
                "
            };
            context.Lectures.Add(lecture2);
            await context.SaveChangesAsync();

            var test2 = new Test
            {
                LectureId = lecture2.Id,
                Title = "Тест по лекции 2: Развёртывание и сеть",
                PassingScorePercent = 70,
                MaxAttempts = 3,
                TimeLimitMinutes = 10
            };
            context.Tests.Add(test2);
            await context.SaveChangesAsync();

            var t2_q1 = new TestQuestion
            {
                TestId = test2.Id,
                QuestionText = "Должна ли СУБД PostgreSQL быть открыта для прямого сетевого подключения студентов?",
                QuestionType = QuestionType.SingleChoice,
                Points = 1
            };
            context.TestQuestions.Add(t2_q1);
            await context.SaveChangesAsync();

            context.AnswerOptions.AddRange(
                new AnswerOption { QuestionId = t2_q1.Id, OptionText = "Нет, запросы к БД должны идти исключительно через бэкенд приложения", IsCorrect = true, OrderIndex = 1 },
                new AnswerOption { QuestionId = t2_q1.Id, OptionText = "Да, студенты должны напрямую подключаться к БД через pgAdmin", IsCorrect = false, OrderIndex = 2 }
            );
            await context.SaveChangesAsync();

            // === TOPIC 2 ===
            var topic2 = new Topic
            {
                DisciplineId = discipline.Id,
                Title = "Тема 2. Контроль качества и приёмочное тестирование",
                Description = "Методики тестирования, выявление дефектов и составление протокола проверок.",
                OrderIndex = 2
            };
            context.Topics.Add(topic2);
            await context.SaveChangesAsync();

            var lecture3 = new Lecture
            {
                TopicId = topic2.Id,
                Title = "Лекция 3. Комплексная проверка и устранение дефектов",
                Description = "12 контрольных проверок веб-приложения перед вводом в эксплуатацию.",
                OrderIndex = 1,
                RequiresTestToUnlockNext = true,
                PassingScorePercent = 70,
                RequiresQuestionsCheck = false,
                Deadline = DateTime.UtcNow.AddDays(21),
                ContentHtml = @"
                    <h4>1. Программа испытаний</h4>
                    <p>При вводе в эксплуатацию программный продукт подвергается функциональному и нагрузочному тестированию:</p>
                    <ul>
                        <li>Проверка ролей и авторизации (Студент, Преподаватель, Администратор);</li>
                        <li>Попытки обхода блокировок лекций по прямым URL-ссылкам;</li>
                        <li>Тестирование многопользовательской работы и сохранения прогресса.</li>
                    </ul>
                "
            };
            context.Lectures.Add(lecture3);
            await context.SaveChangesAsync();

            // === TOPIC 3 ===
            var topic3 = new Topic
            {
                DisciplineId = discipline.Id,
                Title = "Тема 3. Сопровождение и резервное копирование",
                Description = "Регламент поддержки, резервное копирование БД и аварийное восстановление.",
                OrderIndex = 3
            };
            context.Topics.Add(topic3);
            await context.SaveChangesAsync();

            var lecture4 = new Lecture
            {
                TopicId = topic3.Id,
                Title = "Лекция 4. Регламент резервного копирования и аудит событий",
                Description = "Создание дампов PostgreSQL, фиксация действий преподавателя в журнале аудита.",
                OrderIndex = 1,
                RequiresTestToUnlockNext = false,
                PassingScorePercent = 70,
                RequiresQuestionsCheck = false,
                Deadline = DateTime.UtcNow.AddDays(28),
                ContentHtml = @"
                    <h4>1. Регламент сопровождения</h4>
                    <p>Для предотвращения потери учебных данных преподавателя и студентов организуется периодическое создание дампа БД утилитой <code>pg_dump</code> и архивирование каталога загруженных лекций.</p>
                "
            };
            context.Lectures.Add(lecture4);
            await context.SaveChangesAsync();
        }
    }
}
