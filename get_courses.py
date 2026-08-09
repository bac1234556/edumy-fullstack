import sys, psycopg2
sys.stdout.reconfigure(encoding='utf-8')
conn = psycopg2.connect("host=localhost dbname=EduMyDb user=postgres password=postgres")
cur = conn.cursor()
cur.execute('SELECT "CourseId" FROM "Courses";')
print([r[0] for r in cur.fetchall()])
