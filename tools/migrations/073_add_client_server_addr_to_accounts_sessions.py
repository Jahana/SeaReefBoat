import mariadb


def migration_name():
    return "Adding client_server_addr to accounts_sessions (network.ZONE_IP_FROM_CLIENT)"


def check_preconditions(cur):
    return


def needs_to_run(cur):
    cur.execute("SHOW COLUMNS FROM accounts_sessions LIKE 'client_server_addr'")
    if not cur.fetchone():
        return True
    return False


def migrate(cur, db):
    try:
        cur.execute(
            "ALTER TABLE accounts_sessions \
                ADD COLUMN `client_server_addr` int(10) unsigned NOT NULL DEFAULT '0' AFTER `client_port`;"
        )
        db.commit()
    except mariadb.Error as err:
        print("Something went wrong: {}".format(err))