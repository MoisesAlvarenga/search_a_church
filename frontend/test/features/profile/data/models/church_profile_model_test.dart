import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/profile/data/models/church_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/models/meeting_schedule_model.dart';

void main() {
  group('MeetingScheduleModel', () {
    const json = {
      'id': 'sch-1',
      'dayOfWeek': 0,
      'startTime': '10:00',
      'description': 'Culto Matutino',
      'language': 'pt',
    };

    test('fromJson and toJson map correctly', () {
      final model = MeetingScheduleModel.fromJson(json);

      expect(model.id, 'sch-1');
      expect(model.dayOfWeek, 0);
      expect(model.startTime, '10:00');
      expect(model.description, 'Culto Matutino');
      expect(model.language, 'pt');

      final serialized = model.toJson();
      expect(serialized['startTime'], '10:00');
      expect(serialized['id'], 'sch-1');
    });

    test('copyWith works as expected', () {
      final model = MeetingScheduleModel.fromJson(json);
      final updated = model.copyWith(startTime: '11:00');
      expect(updated.startTime, '11:00');
      expect(updated.description, 'Culto Matutino');
    });
  });

  group('ChurchProfileModel', () {
    const rawJson = {
      'id': 'ch-1',
      'placeId': 'place-123',
      'name': 'Igreja Central',
      'address': 'Av. Central, 100',
      'latitude': -23.55,
      'longitude': -46.63,
      'denomination': 'Batista',
      'worshipStyle': 'Contemporâneo',
      'languages': ['pt', 'en'],
      'phone': '+5511999999999',
      'email': 'contato@igreja.org',
      'website': 'https://igreja.org',
      'socialInstagram': '@igreja',
      'socialFacebook': 'igrejaoficial',
      'claimState': 'Verified',
      'verifiedRepresentativeUserId': 'usr-pastor',
      'isActive': true,
      'concurrencyStamp': 'stamp-xyz',
      'tags': ['rampa_acesso', 'estacionamento_proprio'],
      'schedules': [
        {
          'id': 'sch-1',
          'dayOfWeek': 0,
          'startTime': '10:00',
          'description': 'Culto',
          'language': 'pt',
        }
      ],
    };

    test('fromJson deserializes complete entity', () {
      final model = ChurchProfileModel.fromJson(rawJson);

      expect(model.id, 'ch-1');
      expect(model.placeId, 'place-123');
      expect(model.name, 'Igreja Central');
      expect(model.latitude, -23.55);
      expect(model.longitude, -46.63);
      expect(model.languages, ['pt', 'en']);
      expect(model.claimState, 'Verified');
      expect(model.verifiedRepresentativeUserId, 'usr-pastor');
      expect(model.isActive, true);
      expect(model.concurrencyStamp, 'stamp-xyz');
      expect(model.tags, ['rampa_acesso', 'estacionamento_proprio']);
      expect(model.schedules, hasLength(1));
      expect(model.schedules.first.startTime, '10:00');
    });

    test('toJson serializes model correctly', () {
      final model = ChurchProfileModel.fromJson(rawJson);
      final json = model.toJson();

      expect(json['id'], 'ch-1');
      expect(json['placeId'], 'place-123');
      expect(json['concurrencyStamp'], 'stamp-xyz');
      expect(json['tags'], ['rampa_acesso', 'estacionamento_proprio']);
      expect(json['schedules'], hasLength(1));
    });

    test('copyWith updates fields while preserving others', () {
      final model = ChurchProfileModel.fromJson(rawJson);
      final updated = model.copyWith(
        name: 'Novo Nome',
        isActive: false,
      );

      expect(updated.name, 'Novo Nome');
      expect(updated.isActive, false);
      expect(updated.id, model.id);
      expect(updated.concurrencyStamp, model.concurrencyStamp);
    });
  });

  group('UpdateChurchProfileRequestModel', () {
    test('toJson produces expected request dictionary', () {
      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja Atualizada',
        address: 'Rua Nova, 50',
        latitude: -23.5,
        longitude: -46.6,
        concurrencyStamp: 'stamp-123',
        tagCodes: ['rampa_acesso'],
      );

      final json = request.toJson();
      expect(json['name'], 'Igreja Atualizada');
      expect(json['concurrencyStamp'], 'stamp-123');
      expect(json['tagCodes'], ['rampa_acesso']);
    });
  });

  group('UpdateChurchStatusRequestModel and ChurchStatusResponseModel', () {
    test('UpdateChurchStatusRequestModel toJson', () {
      const request = UpdateChurchStatusRequestModel(
        isActive: false,
        concurrencyStamp: 'stamp-456',
      );

      final json = request.toJson();
      expect(json['isActive'], false);
      expect(json['concurrencyStamp'], 'stamp-456');
    });

    test('ChurchStatusResponseModel fromJson', () {
      final json = {
        'churchId': 'ch-1',
        'isActive': false,
        'message': 'Congregação inativada temporariamente',
      };

      final response = ChurchStatusResponseModel.fromJson(json);
      expect(response.churchId, 'ch-1');
      expect(response.isActive, false);
      expect(response.message, contains('inativada'));
    });
  });
}
